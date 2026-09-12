package com.teamfho.domino.security

import com.teamfho.domino.common.ApiAccessDeniedHandler
import com.teamfho.domino.common.ApiAuthenticationEntryPoint
import com.teamfho.domino.common.ApiErrorWriter
import com.teamfho.domino.common.RequestIdFilter
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.HttpMethod
import org.springframework.security.config.annotation.web.builders.HttpSecurity
import org.springframework.security.config.http.SessionCreationPolicy
import org.springframework.security.web.SecurityFilterChain
import org.springframework.security.web.authentication.AnonymousAuthenticationFilter
import org.springframework.security.web.context.SecurityContextHolderFilter

@Configuration(proxyBeanMethods = false)
class SecurityConfiguration {
    @Bean
    fun securityFilterChain(http: HttpSecurity, verifier: FirebaseTokenVerifier, errors: ApiErrorWriter,
        entryPoint: ApiAuthenticationEntryPoint, deniedHandler: ApiAccessDeniedHandler): SecurityFilterChain {
        http.sessionManagement { it.sessionCreationPolicy(SessionCreationPolicy.STATELESS) }
            .formLogin { it.disable() }
            .httpBasic { it.disable() }
            .csrf { it.disable() }
            .logout { it.disable() }
            .requestCache { it.disable() }
            .exceptionHandling { it.authenticationEntryPoint(entryPoint).accessDeniedHandler(deniedHandler) }
            .authorizeHttpRequests {
                it.requestMatchers(HttpMethod.GET, "/actuator/health").permitAll()
                    // Upgrade only; the socket grants no capability until Firebase AUTH succeeds.
                    .requestMatchers(HttpMethod.GET, "/ws/v1/realtime").permitAll()
                    .requestMatchers(HttpMethod.GET, "/actuator/health/realtime").authenticated()
                    .requestMatchers("/api/**").authenticated()
                    .anyRequest().denyAll()
            }
            // Deliberately not beans: Spring Boot must not register them as servlet filters too.
            .addFilterBefore(RequestIdFilter(), SecurityContextHolderFilter::class.java)
            .addFilterBefore(FirebaseAuthenticationFilter(verifier, errors), AnonymousAuthenticationFilter::class.java)
        return http.build()
    }
}
