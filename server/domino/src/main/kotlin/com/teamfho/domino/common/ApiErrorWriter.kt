package com.teamfho.domino.common

import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.slf4j.LoggerFactory
import org.springframework.stereotype.Component
import tools.jackson.databind.ObjectMapper
import java.util.UUID

@Component
class ApiErrorWriter(private val mapper: ObjectMapper) {
    private val log = LoggerFactory.getLogger(javaClass)
    fun write(request: HttpServletRequest, response: HttpServletResponse, code: ApiErrorCode) {
        val id = request.getAttribute(RequestIdFilter.ATTRIBUTE) as? String ?: UUID.randomUUID().toString()
        response.status = code.status
        response.contentType = "application/json"
        response.characterEncoding = "UTF-8"
        response.setHeader("X-Request-ID", id)
        response.setHeader("Cache-Control", "no-store")
        if (code.status == 401) response.setHeader("WWW-Authenticate", "Bearer")
        log.warn("Request rejected category={} requestId={}", code.name, id)
        mapper.writeValue(response.outputStream, ApiErrorResponse(code.name, code.publicMessage, id))
    }
}
