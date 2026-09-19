package com.teamfho.domino.validation

/** Shared by explicit validation/administration executables; not by production requests. */
object RealFirestoreGuard {
    fun requireOptIn(environment:Map<String,String> = System.getenv()) {
        check(environment["DOMINO_REAL_FIRESTORE_TESTS"] == "true") {
            "REAL_FIRESTORE_DISABLED: explicitly set DOMINO_REAL_FIRESTORE_TESTS=true"
        }
    }
}
