package com.teamfho.domino.common

import jakarta.servlet.FilterChain
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.slf4j.MDC
import org.springframework.web.filter.OncePerRequestFilter
import java.util.UUID

class RequestIdFilter : OncePerRequestFilter() {
    override fun doFilterInternal(request: HttpServletRequest, response: HttpServletResponse, filterChain: FilterChain) {
        val previous = MDC.get("requestId")
        val id = UUID.randomUUID().toString()
        request.setAttribute(ATTRIBUTE, id)
        response.setHeader("X-Request-ID", id)
        MDC.put("requestId", id)
        try { filterChain.doFilter(request, response) }
        finally { if (previous == null) MDC.remove("requestId") else MDC.put("requestId", previous) }
    }
    companion object { const val ATTRIBUTE = "domino.requestId" }
}
