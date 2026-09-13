package com.teamfho.domino.economy.reward

import com.google.api.core.ApiFutures
import com.google.cloud.Timestamp
import com.google.cloud.firestore.*
import org.mockito.Mockito.*
import java.time.Instant
import java.util.concurrent.ConcurrentHashMap

// Test double for SDK transactions: staged writes commit together under a lock.
// It verifies adapter calls but does not substitute for emulator contention tests.
internal class RewardFirestoreTransactions(var now: Instant) {
    val firestore: Firestore = mock(Firestore::class.java)
    val documents = ConcurrentHashMap<String, Map<String, Any>>()
    val callbacks = mutableListOf<List<String>>()
    var retryFirstCallback = false
    var injectedFailure: Throwable? = null
    var failAfterCommitOnce = false
    private val refs = ConcurrentHashMap<String, DocumentReference>()
    private val lock = Any()
    private val queries = ConcurrentHashMap<Query, Pair<String,Timestamp>>()

    init {
        `when`(firestore.collection(anyString())).thenAnswer { invocation ->
            val path=invocation.getArgument<String>(0)
            mock(CollectionReference::class.java).also { collection ->
                `when`(collection.whereGreaterThanOrEqualTo(eq("createdAt"), any(Timestamp::class.java))).thenAnswer { filter ->
                    val query=mock(Query::class.java); queries[query]=path to filter.getArgument(1)
                    `when`(query.limit(anyInt())).thenReturn(query); query
                }
            }
        }
        `when`(firestore.document(anyString())).thenAnswer { invocation ->
            val path = invocation.getArgument<String>(0)
            refs.computeIfAbsent(path) { mock(DocumentReference::class.java).also { `when`(it.path).thenReturn(path) } }
        }
        `when`(firestore.runTransaction(any<Transaction.Function<Any>>(), any(TransactionOptions::class.java)))
            .thenAnswer { invocation ->
                synchronized(lock) {
                    try {
                        injectedFailure?.let { throw it }
                        val function = invocation.getArgument<Transaction.Function<Any>>(0)
                        val options = invocation.getArgument<TransactionOptions>(1)
                        check(options.numberOfAttempts == 5)
                        var result: Any? = null
                        repeat(if (retryFirstCallback) 2 else 1) { attempt ->
                            val writes = mutableListOf<Triple<String, String, Map<String, Any>>>()
                            val trace = mutableListOf<String>()
                            val tx = mock(Transaction::class.java)
                            `when`(tx.get(any(Query::class.java))).thenAnswer { read ->
                                check(writes.isEmpty()) { "Read after write" }
                                val (path,cutoff)=queries.getValue(read.getArgument(0))
                                val rows=documents.filter { (key,value) -> key.startsWith("$path/") &&
                                    (value["createdAt"] as? Timestamp)?.compareTo(cutoff)?.let { it >= 0 } == true }.values.map { data ->
                                    mock(QueryDocumentSnapshot::class.java).also { row ->
                                        `when`(row.getString(anyString())).thenAnswer { data[it.getArgument<String>(0)] as? String }
                                        `when`(row.getLong(anyString())).thenAnswer { data[it.getArgument<String>(0)] as? Long }
                                        `when`(row.getTimestamp(anyString())).thenAnswer { data[it.getArgument<String>(0)] as? Timestamp }
                                    }
                                }
                                val snapshot=mock(QuerySnapshot::class.java); `when`(snapshot.documents).thenReturn(rows)
                                trace += "query:$path"; ApiFutures.immediateFuture(snapshot)
                            }
                            `when`(tx.get(any(DocumentReference::class.java))).thenAnswer { read ->
                                check(writes.isEmpty()) { "Read after write" }
                                val path = read.getArgument<DocumentReference>(0).path
                                trace += "read:$path"
                                val data = documents[path]
                                val snapshot = mock(DocumentSnapshot::class.java)
                                `when`(snapshot.exists()).thenReturn(data != null)
                                `when`(snapshot.data).thenReturn(data)
                                ApiFutures.immediateFuture(snapshot)
                            }
                            doAnswer { write ->
                                val path = write.getArgument<DocumentReference>(0).path
                                trace += "create:$path"
                                writes += Triple("create", path, write.getArgument<Map<String, Any>>(1).toMap())
                                tx
                            }.`when`(tx).create(any(DocumentReference::class.java), anyMap<String, Any>())
                            doAnswer { write ->
                                val path = write.getArgument<DocumentReference>(0).path
                                trace += "update:$path"
                                writes += Triple("update", path, write.getArgument<Map<String, Any>>(1).toMap())
                                tx
                            }.`when`(tx).update(any(DocumentReference::class.java), anyMap<String, Any>())
                            doAnswer { write ->
                                val path = write.getArgument<DocumentReference>(0).path
                                trace += "set:$path"
                                writes += Triple("set", path, write.getArgument<Map<String, Any>>(1).toMap())
                                tx
                            }.`when`(tx).set(any(DocumentReference::class.java), anyMap<String, Any>())
                            try { result = function.updateCallback(tx) }
                            finally { callbacks += trace }
                            if (!retryFirstCallback || attempt == 1) {
                                val staged = documents.toMutableMap()
                                for ((operation, path, values) in writes) {
                                    val resolved = values.mapValues { (_, value) ->
                                        if (value == FieldValue.serverTimestamp()) Timestamp.ofTimeSecondsAndNanos(now.epochSecond, now.nano) else value
                                    }
                                    if (operation == "set") { staged[path] = resolved } else if (operation == "create") {
                                        check(!staged.containsKey(path))
                                        staged[path] = resolved
                                    } else {
                                        check(staged.containsKey(path))
                                        staged[path] = staged.getValue(path) + resolved
                                    }
                                }
                                documents.clear(); documents.putAll(staged)
                                if (failAfterCommitOnce) { failAfterCommitOnce = false; throw java.util.concurrent.TimeoutException() }
                            }
                        }
                        ApiFutures.immediateFuture(result)
                    } catch (error: Throwable) { ApiFutures.immediateFailedFuture<Any>(error) }
                }
            }
    }
}
