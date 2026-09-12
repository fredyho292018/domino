package com.teamfho.domino.security

import com.teamfho.domino.common.ApiErrorCode
import com.teamfho.domino.common.ApiErrorWriter
import jakarta.servlet.FilterChain
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.security.core.context.SecurityContextHolder
import org.springframework.web.filter.OncePerRequestFilter
import java.util.Collections

class FirebaseAuthenticationFilter(
    private val verifier: FirebaseTokenVerifier,
    private val errors: ApiErrorWriter
) : OncePerRequestFilter() {
    private val bearer = Regex("^Bearer[ \\t]+([A-Za-z0-9._~+/-]+=*)$", RegexOption.IGNORE_CASE)
    override fun shouldNotFilter(request: HttpServletRequest) =
        request.method == "GET" && request.requestURI == request.contextPath + "/actuator/health"

    override fun doFilterInternal(request: HttpServletRequest, response: HttpServletResponse, filterChain: FilterChain) {
        val headers = Collections.list(request.getHeaders("Authorization"))
        if (headers.isEmpty()) { filterChain.doFilter(request, response); return }
        val identity = try {
            if (headers.size != 1) throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            val header = headers.single()
            if (header.isBlank()) throw AuthFailure(ApiErrorCode.AUTH_TOKEN_MISSING)
            if (header.length > 8192) throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            val token = bearer.matchEntire(header)?.groupValues?.get(1)
                ?: throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            verifier.verify(token)
        } catch (failure: AuthFailure) {
            SecurityContextHolder.clearContext()
            errors.write(request, response, failure.code)
            return
        } catch (_: Exception) {
            SecurityContextHolder.clearContext()
            errors.write(request, response, ApiErrorCode.INTERNAL_ERROR)
            return
        }
        val context = SecurityContextHolder.createEmptyContext()
        context.authentication = FirebaseAuthenticationToken(identity)
        SecurityContextHolder.setContext(context)
        // Do not misclassify downstream controller exceptions as authentication failures.
        filterChain.doFilter(request, response)
    }
}
