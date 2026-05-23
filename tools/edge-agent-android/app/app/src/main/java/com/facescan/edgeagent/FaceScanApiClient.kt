package com.facescan.edgeagent

import android.os.Build
import android.util.Base64
import okhttp3.Authenticator
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.Route
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.security.MessageDigest
import java.time.Instant
import java.util.Locale
import java.util.UUID
import java.util.concurrent.TimeUnit

class FaceScanApiClient {
    data class ApiCallResult(
        val json: JSONObject? = null,
        val retryableFailure: Boolean = false,
        val statusCode: Int? = null
    )

    fun fetchSnapshot(config: EdgeConfig): ByteArray? {
        val timeout = parseInt(config.timeoutSeconds, 8, 3, 60)
        val client = buildClient(timeout, config)

        val requestBuilder = Request.Builder().url(config.snapshotUrl)
        val cameraAuthMode = CameraAuthMode.from(config.cameraAuthType)
        if (config.cameraUser.isNotBlank() && cameraAuthMode == CameraAuthMode.BASIC) {
            val basic = Base64.encodeToString(
                "${config.cameraUser}:${config.cameraPassword}".toByteArray(),
                Base64.NO_WRAP
            )
            requestBuilder.header("Authorization", "Basic $basic")
        }

        client.newCall(requestBuilder.get().build()).execute().use { response ->
            if (!response.isSuccessful) {
                return null
            }
            return response.body?.bytes()
        }
    }

    fun callPreview(imageBytes: ByteArray, config: EdgeConfig): ApiCallResult {
        return callScanApi("/Scan/Preview", imageBytes, config)
    }

    fun callVerify(imageBytes: ByteArray, config: EdgeConfig): ApiCallResult {
        return callScanApi("/Scan/Verify", imageBytes, config)
    }

    fun sendHeartbeat(config: EdgeConfig, message: String): Boolean {
        val timeout = parseInt(config.timeoutSeconds, 8, 3, 60)
        val client = buildServerClient(timeout)

        val form = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart("StationCode", config.stationCode)
            .addFormDataPart("StationToken", config.stationToken)
            .addFormDataPart("AgentId", buildAgentId(config.stationCode))
            .addFormDataPart("Message", message)
            .addFormDataPart("CapturedAtUtc", Instant.now().toString())
            .build()

        val request = Request.Builder()
            .url("${config.serverUrl.trimEnd('/')}/EdgeAgent/Heartbeat")
            .post(form)
            .build()

        client.newCall(request).execute().use { response ->
            return response.isSuccessful
        }
    }

    fun isPreviewCandidate(previewJson: JSONObject?, minConfidence: Double): Boolean {
        if (previewJson == null) {
            return false
        }
        if (!previewJson.optBoolean("success", false)) {
            return false
        }
        val confidence = previewJson.optDouble("confidence", -1.0)
        return confidence >= minConfidence
    }

    private fun callScanApi(endpoint: String, imageBytes: ByteArray, config: EdgeConfig): ApiCallResult {
        val timeout = parseInt(config.timeoutSeconds, 8, 3, 60)
        val client = buildServerClient(timeout)

        val imageBody = imageBytes.toRequestBody("image/jpeg".toMediaType())
        val form = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart("StationCode", config.stationCode)
            .addFormDataPart("StationToken", config.stationToken)
            .addFormDataPart("RecognitionProfile", "auto")
            .addFormDataPart("ClientCapturedAtLocal", Instant.now().toString())
            .addFormDataPart("Image", "frame.jpg", imageBody)
            .build()

        val request = Request.Builder()
            .url("${config.serverUrl.trimEnd('/')}$endpoint")
            .post(form)
            .build()

        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) {
                return ApiCallResult(
                    json = null,
                    retryableFailure = isRetryableStatus(response.code),
                    statusCode = response.code
                )
            }
            val body = response.body?.string() ?: return ApiCallResult(
                json = null,
                retryableFailure = true,
                statusCode = response.code
            )
            val json = runCatching { JSONObject(body) }.getOrNull()
            return ApiCallResult(json = json, retryableFailure = false, statusCode = response.code)
        }
    }

    private fun buildServerClient(timeoutSeconds: Int): OkHttpClient {
        return OkHttpClient.Builder()
            .connectTimeout(timeoutSeconds.toLong(), TimeUnit.SECONDS)
            .readTimeout(timeoutSeconds.toLong(), TimeUnit.SECONDS)
            .writeTimeout(timeoutSeconds.toLong(), TimeUnit.SECONDS)
            .build()
    }

    private fun buildClient(timeoutSeconds: Int, config: EdgeConfig): OkHttpClient {
        val mode = CameraAuthMode.from(config.cameraAuthType)
        val builder = OkHttpClient.Builder()
            .connectTimeout(timeoutSeconds.toLong(), TimeUnit.SECONDS)
            .readTimeout(timeoutSeconds.toLong(), TimeUnit.SECONDS)
            .writeTimeout(timeoutSeconds.toLong(), TimeUnit.SECONDS)

        if (config.cameraUser.isNotBlank() && mode != CameraAuthMode.NONE) {
            builder.authenticator(CameraHttpAuthenticator(config.cameraUser, config.cameraPassword, mode))
        }

        return builder.build()
    }

    private fun buildAgentId(stationCode: String): String {
        val model = Build.MODEL ?: "android"
        return "$model-$stationCode"
    }

    private fun parseInt(raw: String, fallback: Int, min: Int, max: Int): Int {
        val value = raw.toIntOrNull() ?: fallback
        return value.coerceIn(min, max)
    }

    private fun isRetryableStatus(statusCode: Int): Boolean {
        return statusCode >= 500 || statusCode == 408 || statusCode == 429
    }

    private enum class CameraAuthMode {
        AUTO,
        BASIC,
        DIGEST,
        NONE;

        companion object {
            fun from(value: String): CameraAuthMode {
                return when (value.trim().lowercase(Locale.ROOT)) {
                    "basic" -> BASIC
                    "digest" -> DIGEST
                    "none" -> NONE
                    else -> AUTO
                }
            }
        }
    }

    private class CameraHttpAuthenticator(
        private val username: String,
        private val password: String,
        private val mode: CameraAuthMode
    ) : Authenticator {
        override fun authenticate(route: Route?, response: Response): Request? {
            if (responseCount(response) >= 3) {
                return null
            }

            val challengeHeader = response.header("WWW-Authenticate") ?: return null
            val lowered = challengeHeader.lowercase(Locale.ROOT)

            if ((mode == CameraAuthMode.AUTO || mode == CameraAuthMode.DIGEST) && lowered.contains("digest")) {
                return buildDigestRequest(response, challengeHeader)
            }

            if ((mode == CameraAuthMode.AUTO || mode == CameraAuthMode.BASIC) && lowered.contains("basic")) {
                val basic = Base64.encodeToString("$username:$password".toByteArray(), Base64.NO_WRAP)
                return response.request.newBuilder()
                    .header("Authorization", "Basic $basic")
                    .build()
            }

            return null
        }

        private fun buildDigestRequest(response: Response, challengeHeader: String): Request? {
            val params = parseDigestParams(challengeHeader)
            val realm = params["realm"] ?: return null
            val nonce = params["nonce"] ?: return null
            val qop = params["qop"]?.split(',')?.firstOrNull()?.trim()?.lowercase(Locale.ROOT)
            val opaque = params["opaque"]

            val uri = response.request.url.encodedPath.let { path ->
                val query = response.request.url.encodedQuery
                if (query.isNullOrBlank()) path else "$path?$query"
            }

            val nc = "00000001"
            val cnonce = UUID.randomUUID().toString().replace("-", "").take(16)
            val ha1 = md5("$username:$realm:$password")
            val ha2 = md5("${response.request.method}:$uri")

            val responseDigest = if (!qop.isNullOrBlank()) {
                md5("$ha1:$nonce:$nc:$cnonce:$qop:$ha2")
            } else {
                md5("$ha1:$nonce:$ha2")
            }

            val authorization = StringBuilder().apply {
                append("Digest ")
                append("username=\"$username\", ")
                append("realm=\"$realm\", ")
                append("nonce=\"$nonce\", ")
                append("uri=\"$uri\", ")
                append("response=\"$responseDigest\", ")
                append("algorithm=MD5")
                if (!qop.isNullOrBlank()) {
                    append(", qop=$qop, nc=$nc, cnonce=\"$cnonce\"")
                }
                if (!opaque.isNullOrBlank()) {
                    append(", opaque=\"$opaque\"")
                }
            }.toString()

            return response.request.newBuilder()
                .header("Authorization", authorization)
                .build()
        }

        private fun parseDigestParams(header: String): Map<String, String> {
            val digestPart = header.substringAfter("Digest", "").trim()
            if (digestPart.isBlank()) {
                return emptyMap()
            }

            return digestPart
                .split(',')
                .mapNotNull { token ->
                    val idx = token.indexOf('=')
                    if (idx <= 0) {
                        null
                    } else {
                        val key = token.substring(0, idx).trim().lowercase(Locale.ROOT)
                        val value = token.substring(idx + 1).trim().trim('"')
                        key to value
                    }
                }
                .toMap()
        }

        private fun md5(input: String): String {
            val bytes = MessageDigest.getInstance("MD5").digest(input.toByteArray())
            return bytes.joinToString("") { "%02x".format(it) }
        }

        private fun responseCount(response: Response): Int {
            var current: Response? = response
            var count = 1
            while (current?.priorResponse != null) {
                count += 1
                current = current.priorResponse
            }
            return count
        }
    }
}
