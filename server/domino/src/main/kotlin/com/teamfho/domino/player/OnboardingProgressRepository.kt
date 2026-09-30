package com.teamfho.domino.player

import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.TransactionOptions
import java.util.concurrent.TimeUnit

interface OnboardingProgressTransaction {
    fun read(path: String): Map<String,Any>?
    fun write(path: String, value: Map<String,Any>)
}
interface OnboardingProgressRepository {
    fun <T> transaction(action: (OnboardingProgressTransaction)->T): T
}
class FirestoreOnboardingProgressRepository(private val db: Firestore): OnboardingProgressRepository {
    override fun <T> transaction(action: (OnboardingProgressTransaction)->T): T {
        try {
            return db.runTransaction({ tx ->
                val pending=linkedMapOf<String,Map<String,Any>>()
                val result=action(object:OnboardingProgressTransaction {
                    override fun read(path:String)=tx.get(db.document(path)).get().data
                    override fun write(path:String,value:Map<String,Any>) { pending[path]=value }
                })
                pending.forEach { (path,value)->tx.set(db.document(path),value) }
                result
            },TransactionOptions.createReadWriteOptionsBuilder().setNumberOfAttempts(5).build()).get(30,TimeUnit.SECONDS)
        } catch(e: Exception) {
            var cause: Throwable=e
            while(cause.cause != null && cause.cause !== cause) cause=cause.cause!!
            if(cause is OnboardingFailure) throw cause
            if(e is InterruptedException) Thread.currentThread().interrupt()
            if(cause is com.google.api.gax.rpc.AbortedException) throw OnboardingFailure("FIRESTORE_CONTENTION_EXHAUSTED",503)
            throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
        }
    }
}
