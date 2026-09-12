package com.teamfho.domino.common

import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.security.access.AccessDeniedException
import org.springframework.security.web.access.AccessDeniedHandler
import org.springframework.stereotype.Component

@Component
class ApiAccessDeniedHandler(private val errors: ApiErrorWriter) : AccessDeniedHandler {
    override fun handle(request: HttpServletRequest, response: HttpServletResponse, exception: AccessDeniedException) =
        errors.write(request, response, ApiErrorCode.ACCESS_DENIED)
}
