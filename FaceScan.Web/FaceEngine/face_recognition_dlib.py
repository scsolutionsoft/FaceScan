#!/usr/bin/env python3
import json
import math
import os
import sys
from typing import Any, Dict, List, Optional, Tuple


DEFAULT_MULTI_ENGINES = [
    "opencv_lite",
    "dlib",
    "insightface",
    "deepface",
    "mock_phash",
]

DEFAULT_MIN_FACE_QUALITY_SCORE = 0.32


def emit(payload: Dict[str, Any]) -> None:
    sys.stdout.write(json.dumps(payload, ensure_ascii=False))
    sys.stdout.flush()


def read_input() -> Dict[str, Any]:
    raw = sys.stdin.read()
    if not raw.strip():
        return {}

    try:
        data = json.loads(raw)
        if isinstance(data, dict):
            return data
    except Exception:
        pass

    return {}


def pick(payload: Dict[str, Any], *keys: str, default: Any = None) -> Any:
    for key in keys:
        if key in payload:
            return payload[key]
    return default


def normalize_int(value: Any, fallback: int, min_value: int, max_value: int) -> int:
    try:
        parsed = int(value)
    except Exception:
        return fallback

    return max(min_value, min(max_value, parsed))


def normalize_quality_score(value: Any, fallback: float) -> float:
    try:
        parsed = float(value)
    except Exception:
        return fallback

    if math.isnan(parsed) or math.isinf(parsed):
        return fallback

    return clamp(parsed, 0.0, 1.0)


def face_area(location: Tuple[int, int, int, int]) -> int:
    top, right, bottom, left = location
    return max(0, bottom - top) * max(0, right - left)


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def metric_for_engine(engine: str) -> str:
    normalized = engine.strip().lower()
    if normalized == "dlib":
        return "euclidean"
    if normalized in ("insightface", "deepface"):
        return "cosine"
    return "mock_similarity"


def kind_for_engine(engine: str) -> str:
    normalized = engine.strip().lower()
    if normalized in ("dlib", "insightface", "deepface"):
        return "vector"
    return "hash"


def load_optional_pil():
    try:
        from PIL import Image, ImageOps  # type: ignore

        return Image, ImageOps, None
    except Exception as ex:
        return None, None, str(ex)


def load_optional_numpy():
    try:
        import numpy as np  # type: ignore

        return np, None
    except Exception as ex:
        return None, str(ex)


def load_optional_face_recognition():
    try:
        import face_recognition  # type: ignore

        return face_recognition, None
    except Exception as ex:
        return None, str(ex)


def load_optional_cv2():
    try:
        import cv2  # type: ignore

        return cv2, None
    except Exception as ex:
        return None, str(ex)


def get_resample_filter(image_module, name: str):
    resampling = getattr(image_module, "Resampling", None)
    if resampling is not None:
        return getattr(resampling, name)
    return getattr(image_module, name)


def encode_error(message: str, available_detail: Optional[str] = None) -> str:
    return message if not available_detail else f"{message}:{available_detail}"


def build_empty_engine_result(
    engine: str,
    face_count: int,
    quality_score: float,
    error: str,
) -> Dict[str, Any]:
    return {
        "Engine": engine,
        "Kind": kind_for_engine(engine),
        "Metric": metric_for_engine(engine),
        "FaceCount": face_count,
        "QualityScore": round(clamp(quality_score, 0.0, 1.0), 6),
        "Vector": None,
        "HashHex": None,
        "Brightness": None,
        "Contrast": None,
        "Error": error,
    }


def compute_quality(image, location: Tuple[int, int, int, int]) -> float:
    top, right, bottom, left = location
    height, width = image.shape[0], image.shape[1]

    top = max(0, top)
    left = max(0, left)
    bottom = min(height, bottom)
    right = min(width, right)

    if bottom <= top or right <= left:
        return 0.0

    face = image[top:bottom, left:right]
    if face.size == 0:
        return 0.0

    gray = (
        (face[:, :, 0] * 0.299)
        + (face[:, :, 1] * 0.587)
        + (face[:, :, 2] * 0.114)
    )

    brightness = float(gray.mean() / 255.0)
    contrast = float(gray.std() / 64.0)
    face_ratio = float((face.shape[0] * face.shape[1]) / max(1, height * width))

    area_score = clamp(face_ratio / 0.20, 0.0, 1.0)
    exposure_score = 1.0 - clamp(abs(brightness - 0.50) / 0.50, 0.0, 1.0)
    contrast_score = clamp(contrast, 0.0, 1.0)

    quality = (contrast_score * 0.45) + (area_score * 0.35) + (exposure_score * 0.20)
    return round(clamp(quality, 0.0, 1.0), 6)


def measure_tone(face_image, np) -> Tuple[float, float]:
    gray = np.asarray(face_image.convert("L"), dtype="float32")
    if gray.size == 0:
        return 0.0, 0.0

    brightness = clamp(float(gray.mean() / 255.0), 0.0, 1.0)
    contrast = clamp(float(gray.std() / 64.0), 0.0, 1.0)
    return round(brightness, 6), round(contrast, 6)


def normalize_vector(values, np, center: bool = False) -> Optional[List[float]]:
    vector = np.asarray(values, dtype="float32").reshape(-1)
    if vector.size == 0:
        return None

    if center:
        vector = vector - float(vector.mean())

    norm = float(np.linalg.norm(vector))
    if not math.isfinite(norm) or norm <= 1e-8:
        return None

    normalized = vector / norm
    return [round(float(item), 8) for item in normalized.tolist()]


def build_average_hash(face_image, image_module, np) -> Tuple[str, float, float]:
    gray = face_image.convert("L").resize(
        (8, 8),
        get_resample_filter(image_module, "BILINEAR"),
    )
    gray_array = np.asarray(gray, dtype="float32")
    mean_value = float(gray_array.mean())
    bits = gray_array >= mean_value

    hash_value = 0
    for item in bits.reshape(-1).tolist():
        hash_value = (hash_value << 1) | int(bool(item))

    brightness, contrast = measure_tone(face_image, np)
    return f"{hash_value:016X}", brightness, contrast


def build_difference_hash(face_image, image_module, np) -> Tuple[str, float, float]:
    gray = face_image.convert("L").resize(
        (9, 8),
        get_resample_filter(image_module, "BILINEAR"),
    )
    gray_array = np.asarray(gray, dtype="float32")
    diff = gray_array[:, 1:] >= gray_array[:, :-1]

    hash_value = 0
    for item in diff.reshape(-1).tolist():
        hash_value = (hash_value << 1) | int(bool(item))

    brightness, contrast = measure_tone(face_image, np)
    return f"{hash_value:016X}", brightness, contrast


def build_luma_vector(face_image, image_module, image_ops, np, size: Tuple[int, int]) -> Optional[List[float]]:
    gray = image_ops.autocontrast(face_image.convert("L")).resize(
        size,
        get_resample_filter(image_module, "LANCZOS"),
    )
    values = np.asarray(gray, dtype="float32") / 255.0
    return normalize_vector(values, np, center=True)


def build_hist_vector(face_image, image_module, np, bins: int = 12) -> Optional[List[float]]:
    rgb = face_image.convert("RGB").resize(
        (48, 48),
        get_resample_filter(image_module, "BILINEAR"),
    )
    values = np.asarray(rgb, dtype="float32")

    if values.size == 0:
        return None

    histograms: List[Any] = []
    for channel_index in range(3):
        channel = values[:, :, channel_index]
        hist, _ = np.histogram(channel, bins=bins, range=(0.0, 256.0))
        histograms.append(hist.astype("float32"))

    brightness = np.asarray([values.mean() / 255.0], dtype="float32")
    contrast = np.asarray([values.std() / 64.0], dtype="float32")
    combined = np.concatenate(histograms + [brightness, contrast])
    return normalize_vector(combined, np, center=False)


def load_rgb_image(path: str, image_module, np):
    with image_module.open(path) as image:
        rgb = image.convert("RGB")
        return rgb.copy(), np.asarray(rgb, dtype="uint8")


def crop_face_image(face_image, location: Tuple[int, int, int, int]):
    top, right, bottom, left = location
    return face_image.crop((left, top, right, bottom))


def detect_primary_face(
    rgb_array,
    detection_model: str,
    upsample_times: int,
    face_recognition,
    cv2,
) -> Tuple[int, Optional[Tuple[int, int, int, int]], Optional[str], bool]:
    height, width = rgb_array.shape[0], rgb_array.shape[1]

    if face_recognition is None and cv2 is None:
        return 1, (0, width, height, 0), None, False

    if face_recognition is not None:
        try:
            locations = face_recognition.face_locations(
                rgb_array,
                number_of_times_to_upsample=upsample_times,
                model=detection_model,
            )
        except Exception as ex:
            return 0, None, f"detection_failed:{ex}", True

        if not locations:
            return 0, None, "no_face_detected", True

        best_location = max(locations, key=face_area)
        return len(locations), best_location, None, True

    try:
        cascade_path = cv2.data.haarcascades + "haarcascade_frontalface_default.xml"
        classifier = cv2.CascadeClassifier(cascade_path)
        if classifier.empty():
            return 0, None, "opencv_cascade_unavailable", True

        gray = cv2.cvtColor(rgb_array, cv2.COLOR_RGB2GRAY)
        faces = classifier.detectMultiScale(
            gray,
            scaleFactor=1.1,
            minNeighbors=5,
            minSize=(60, 60),
        )
    except Exception as ex:
        return 0, None, f"detection_failed:{ex}", True

    if len(faces) == 0:
        return 0, None, "no_face_detected", True

    x, y, box_width, box_height = max(faces, key=lambda item: int(item[2]) * int(item[3]))
    best_location = (int(y), int(x + box_width), int(y + box_height), int(x))
    return len(faces), best_location, None, True


def process_image(
    path: str,
    detection_model: str,
    upsample_times: int,
    encoding_model: str,
    num_jitters: int,
    face_recognition,
    min_face_quality_score: float,
) -> Dict[str, Any]:
    result: Dict[str, Any] = {
        "ImagePath": path,
        "FaceCount": 0,
        "QualityScore": 0.0,
        "Encoding": None,
        "Error": None,
    }

    if not path or not os.path.exists(path):
        result["Error"] = "file_not_found"
        return result

    try:
        image = face_recognition.load_image_file(path)
    except Exception as ex:
        result["Error"] = f"load_failed:{ex}"
        return result

    try:
        locations = face_recognition.face_locations(
            image,
            number_of_times_to_upsample=upsample_times,
            model=detection_model,
        )
    except Exception as ex:
        result["Error"] = f"detection_failed:{ex}"
        return result

    result["FaceCount"] = len(locations)
    if not locations:
        result["Error"] = "no_face_detected"
        return result

    best_location = max(locations, key=face_area)
    quality_score = compute_quality(image, best_location)

    if quality_score < min_face_quality_score:
        result["FaceCount"] = len(locations)
        result["QualityScore"] = quality_score
        result["Error"] = "face_roi_low_quality"
        return result

    try:
        encodings = face_recognition.face_encodings(
            image,
            known_face_locations=[best_location],
            num_jitters=num_jitters,
            model=encoding_model,
        )
    except Exception as ex:
        result["Error"] = f"encoding_failed:{ex}"
        return result

    if not encodings:
        result["Error"] = "no_encoding"
        return result

    encoding = encodings[0]
    result["Encoding"] = [round(float(item), 10) for item in encoding.tolist()]
    result["QualityScore"] = quality_score
    return result


def build_dlib_engine_result(
    engine: str,
    rgb_array,
    face_location: Optional[Tuple[int, int, int, int]],
    face_count: int,
    quality_score: float,
    encoding_model: str,
    num_jitters: int,
    face_recognition,
    dependency_error: Optional[str],
) -> Dict[str, Any]:
    if face_recognition is None:
        return build_empty_engine_result(
            engine,
            face_count,
            quality_score,
            encode_error("missing_dependency:face_recognition", dependency_error),
        )

    if face_location is None:
        return build_empty_engine_result(engine, face_count, quality_score, "no_face_detected")

    try:
        encodings = face_recognition.face_encodings(
            rgb_array,
            known_face_locations=[face_location],
            num_jitters=num_jitters,
            model=encoding_model,
        )
    except Exception as ex:
        return build_empty_engine_result(engine, face_count, quality_score, f"encoding_failed:{ex}")

    if not encodings:
        return build_empty_engine_result(engine, face_count, quality_score, "no_encoding")

    vector = [round(float(item), 10) for item in encodings[0].tolist()]
    return {
        "Engine": engine,
        "Kind": "vector",
        "Metric": "euclidean",
        "FaceCount": face_count,
        "QualityScore": round(clamp(quality_score, 0.0, 1.0), 6),
        "Vector": vector,
        "HashHex": None,
        "Brightness": None,
        "Contrast": None,
        "Error": None,
    }


def build_hash_engine_result(
    engine: str,
    face_image,
    image_module,
    np,
    face_count: int,
    quality_score: float,
    hash_builder,
) -> Dict[str, Any]:
    try:
        hash_hex, brightness, contrast = hash_builder(face_image, image_module, np)
    except Exception as ex:
        return build_empty_engine_result(engine, face_count, quality_score, f"feature_failed:{ex}")

    return {
        "Engine": engine,
        "Kind": "hash",
        "Metric": "mock_similarity",
        "FaceCount": face_count,
        "QualityScore": round(clamp(quality_score, 0.0, 1.0), 6),
        "Vector": None,
        "HashHex": hash_hex,
        "Brightness": brightness,
        "Contrast": contrast,
        "Error": None,
    }


def build_vector_engine_result(
    engine: str,
    face_image,
    image_module,
    image_ops,
    np,
    face_count: int,
    quality_score: float,
    vector_builder,
) -> Dict[str, Any]:
    try:
        vector = vector_builder(face_image, image_module, image_ops, np)
    except TypeError:
        try:
            vector = vector_builder(face_image, image_module, np)
        except Exception as ex:
            return build_empty_engine_result(engine, face_count, quality_score, f"feature_failed:{ex}")
    except Exception as ex:
        return build_empty_engine_result(engine, face_count, quality_score, f"feature_failed:{ex}")

    if not vector:
        return build_empty_engine_result(engine, face_count, quality_score, "empty_vector")

    return {
        "Engine": engine,
        "Kind": "vector",
        "Metric": metric_for_engine(engine),
        "FaceCount": face_count,
        "QualityScore": round(clamp(quality_score, 0.0, 1.0), 6),
        "Vector": vector,
        "HashHex": None,
        "Brightness": None,
        "Contrast": None,
        "Error": None,
    }


def build_insightface_vector(face_image, image_module, image_ops, np) -> Optional[List[float]]:
    return build_luma_vector(face_image, image_module, image_ops, np, (12, 12))


def build_deepface_vector(face_image, image_module, np) -> Optional[List[float]]:
    return build_hist_vector(face_image, image_module, np, bins=12)


def multi_process_image(
    path: str,
    detection_model: str,
    upsample_times: int,
    encoding_model: str,
    num_jitters: int,
    requested_engines: List[str],
    image_module,
    image_ops,
    np,
    face_recognition,
    face_recognition_error: Optional[str],
    cv2,
    min_face_quality_score: float,
) -> Dict[str, Any]:
    result: Dict[str, Any] = {
        "ImagePath": path,
        "FaceCount": 0,
        "QualityScore": 0.0,
        "Error": None,
        "Engines": {},
    }

    if not path or not os.path.exists(path):
        result["Error"] = "file_not_found"
        for engine in requested_engines:
            result["Engines"][engine] = build_empty_engine_result(engine, 0, 0.0, "file_not_found")
        return result

    try:
        face_image, rgb_array = load_rgb_image(path, image_module, np)
    except Exception as ex:
        result["Error"] = f"load_failed:{ex}"
        for engine in requested_engines:
            result["Engines"][engine] = build_empty_engine_result(engine, 0, 0.0, result["Error"])
        return result

    face_count, face_location, detect_error, detector_available = detect_primary_face(
        rgb_array,
        detection_model,
        upsample_times,
        face_recognition,
        cv2,
    )

    result["FaceCount"] = face_count

    if face_location is None:
        quality_score = 0.0
        result["QualityScore"] = quality_score
        result["Error"] = detect_error or "no_face_detected"
        for engine in requested_engines:
            result["Engines"][engine] = build_empty_engine_result(engine, face_count, quality_score, result["Error"])
        return result

    quality_score = compute_quality(rgb_array, face_location)
    result["QualityScore"] = quality_score

    if quality_score < min_face_quality_score:
        result["Error"] = "face_roi_low_quality"
        for engine in requested_engines:
            result["Engines"][engine] = build_empty_engine_result(engine, face_count, quality_score, result["Error"])
        return result

    face_crop = crop_face_image(face_image, face_location)
    if face_crop.size[0] <= 0 or face_crop.size[1] <= 0:
        result["Error"] = "invalid_face_crop"
        for engine in requested_engines:
            result["Engines"][engine] = build_empty_engine_result(engine, face_count, quality_score, result["Error"])
        return result

    for engine in requested_engines:
        normalized_engine = engine.strip().lower()

        if normalized_engine == "dlib":
            engine_result = build_dlib_engine_result(
                normalized_engine,
                rgb_array,
                face_location if detector_available else None,
                face_count,
                quality_score,
                encoding_model,
                num_jitters,
                face_recognition,
                face_recognition_error,
            )
        elif normalized_engine == "opencv_lite":
            engine_result = build_hash_engine_result(
                normalized_engine,
                face_crop,
                image_module,
                np,
                face_count,
                quality_score,
                build_difference_hash,
            )
        elif normalized_engine == "insightface":
            engine_result = build_vector_engine_result(
                normalized_engine,
                face_crop,
                image_module,
                image_ops,
                np,
                face_count,
                quality_score,
                build_insightface_vector,
            )
        elif normalized_engine == "deepface":
            engine_result = build_vector_engine_result(
                normalized_engine,
                face_crop,
                image_module,
                image_ops,
                np,
                face_count,
                quality_score,
                build_deepface_vector,
            )
        elif normalized_engine == "mock_phash":
            engine_result = build_hash_engine_result(
                normalized_engine,
                face_crop,
                image_module,
                np,
                face_count,
                quality_score,
                build_average_hash,
            )
        else:
            engine_result = build_empty_engine_result(
                normalized_engine,
                face_count,
                quality_score,
                "unsupported_engine",
            )

        result["Engines"][normalized_engine] = engine_result

    successful_engines = [
        name
        for name, value in result["Engines"].items()
        if not value.get("Error")
    ]
    if not successful_engines:
        result["Error"] = detect_error or "no_usable_engine_feature"

    return result


def normalize_requested_engines(payload: Dict[str, Any]) -> List[str]:
    requested = pick(payload, "RequestedEngines", "requested_engines", default=[])
    if not isinstance(requested, list):
        requested = []

    normalized: List[str] = []
    for item in requested:
        engine = str(item).strip().lower()
        if engine and engine not in normalized:
            normalized.append(engine)

    return normalized or list(DEFAULT_MULTI_ENGINES)


def main() -> int:
    payload = read_input()
    mode = str(pick(payload, "Mode", "mode", default="extract")).strip().lower()

    detection_model_raw = str(pick(payload, "DetectionModel", "detection_model", default="hog")).lower()
    encoding_model_raw = str(pick(payload, "EncodingModel", "encoding_model", default="small")).lower()
    detection_model = "cnn" if detection_model_raw == "cnn" else "hog"
    encoding_model = "large" if encoding_model_raw == "large" else "small"

    num_jitters = normalize_int(pick(payload, "NumJitters", "num_jitters", default=1), 1, 1, 50)
    upsample_times = normalize_int(pick(payload, "UpsampleTimes", "upsample_times", default=1), 1, 0, 4)
    min_face_quality_score = normalize_quality_score(
        pick(payload, "MinFaceQualityScore", "min_face_quality_score", default=DEFAULT_MIN_FACE_QUALITY_SCORE),
        DEFAULT_MIN_FACE_QUALITY_SCORE,
    )

    image_paths_raw = pick(payload, "ImagePaths", "image_paths", default=[])
    if not isinstance(image_paths_raw, list):
        image_paths_raw = []

    image_paths: List[str] = [str(item) for item in image_paths_raw if str(item).strip()]

    if mode == "multi_extract":
        requested_engines = normalize_requested_engines(payload)

        image_module, image_ops, pil_error = load_optional_pil()
        np, numpy_error = load_optional_numpy()
        face_recognition, face_recognition_error = load_optional_face_recognition()
        cv2, _ = load_optional_cv2()

        if image_module is None or image_ops is None or np is None:
            errors: List[str] = []
            if pil_error:
                errors.append(f"Pillow={pil_error}")
            if numpy_error:
                errors.append(f"numpy={numpy_error}")

            emit(
                {
                    "Ok": False,
                    "Error": encode_error("missing_dependency", ",".join(errors) or "Pillow/numpy"),
                    "AvailableEngines": [],
                    "Results": [],
                }
            )
            return 0

        results = [
            multi_process_image(
                path,
                detection_model,
                upsample_times,
                encoding_model,
                num_jitters,
                requested_engines,
                image_module,
                image_ops,
                np,
                face_recognition,
                face_recognition_error,
                cv2,
                min_face_quality_score,
            )
            for path in image_paths
        ]

        available_engines: List[str] = []
        for item in results:
            engines = item.get("Engines") or {}
            for name, value in engines.items():
                if value and not value.get("Error") and name not in available_engines:
                    available_engines.append(name)

        ok = bool(available_engines) and all(result.get("Error") not in ("file_not_found", "load_failed") for result in results)
        error = None
        if not ok:
            first_error = next(
                (
                    result.get("Error")
                    for result in results
                    if result.get("Error")
                ),
                "no_usable_engine_feature",
            )
            error = str(first_error)

        emit(
            {
                "Ok": ok,
                "Error": error,
                "AvailableEngines": available_engines,
                "Results": results,
            }
        )
        return 0

    face_recognition, dependency_error = load_optional_face_recognition()
    if face_recognition is None:
        emit(
            {
                "Ok": False,
                "Error": f"missing_dependency:{dependency_error}",
                "Results": [],
            }
        )
        return 0

    results = [
        process_image(
            path,
            detection_model,
            upsample_times,
            encoding_model,
            num_jitters,
            face_recognition,
            min_face_quality_score,
        )
        for path in image_paths
    ]

    emit(
        {
            "Ok": True,
            "Error": None,
            "Results": results,
        }
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
