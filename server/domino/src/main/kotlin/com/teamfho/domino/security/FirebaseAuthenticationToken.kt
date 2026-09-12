package com.teamfho.domino.security

import org.springframework.security.authentication.AbstractAuthenticationToken

class FirebaseAuthenticationToken(private val identity: FirebaseIdentity) : AbstractAuthenticationToken(emptyList()) {
    init { super.setAuthenticated(true) }
    override fun getPrincipal(): FirebaseIdentity = identity
    override fun getCredentials(): Any? = null
    override fun getName(): String = identity.uid
    override fun setAuthenticated(authenticated: Boolean) {
        require(!authenticated) { "Create an authentication from a verified identity." }
        super.setAuthenticated(false)
    }
}
