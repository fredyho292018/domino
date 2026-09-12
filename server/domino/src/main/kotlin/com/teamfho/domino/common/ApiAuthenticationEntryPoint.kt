package com.teamfho.domino.common

import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.security.core.AuthenticationException
import org.springframework.security.web.AuthenticationEntryPoint
import org.springframework.stereotype.Component

@Component
class ApiAuthenticationEntryPoint(private val errors: ApiErrorWriter) : AuthenticationEntryPoint {
    override fun commence(request: HttpServletRequest, response: HttpServletResponse, exception: AuthenticationException) =
        errors.write(request, response, ApiErrorCode.AUTH_TOKEN_MISSING)
}
