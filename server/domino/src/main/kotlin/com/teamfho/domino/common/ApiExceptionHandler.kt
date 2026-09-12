package com.teamfho.domino.common

import com.teamfho.domino.player.FoundationError
import com.teamfho.domino.player.DisplayNameException
import com.teamfho.domino.player.PlayerFoundationException
import com.teamfho.domino.player.UnsupportedPlayerLanguageException
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.http.converter.HttpMessageNotReadableException
import org.springframework.security.access.AccessDeniedException
import org.springframework.security.core.AuthenticationException
import org.springframework.web.bind.annotation.ExceptionHandler
import org.springframework.web.bind.annotation.RestControllerAdvice

@RestControllerAdvice
class ApiExceptionHandler(private val errors: ApiErrorWriter) {
    @ExceptionHandler(Exception::class)
    fun handle(exception: Exception, request: HttpServletRequest, response: HttpServletResponse) {
        val code = when (exception) {
            is DisplayNameException -> if (exception.reserved) ApiErrorCode.DISPLAY_NAME_RESERVED else ApiErrorCode.DISPLAY_NAME_INVALID
            is HttpMessageNotReadableException -> ApiErrorCode.REQUEST_INVALID
            is UnsupportedPlayerLanguageException -> ApiErrorCode.LANGUAGE_UNSUPPORTED
            is PlayerFoundationException -> when (exception.code) {
                FoundationError.PLAYER_STATE_CONFLICT -> ApiErrorCode.PLAYER_STATE_CONFLICT
                FoundationError.WALLET_STATE_INVALID -> ApiErrorCode.WALLET_STATE_INVALID
                FoundationError.FIRESTORE_UNAVAILABLE -> ApiErrorCode.DEPENDENCY_UNAVAILABLE
                FoundationError.FIRESTORE_CONTENTION_EXHAUSTED -> ApiErrorCode.FIRESTORE_CONTENTION_EXHAUSTED
            }
            // Leave security exceptions to the existing filter-chain handlers.
            is AccessDeniedException, is AuthenticationException -> throw exception
            else -> ApiErrorCode.INTERNAL_ERROR
        }
        errors.write(request, response, code)
    }
}
