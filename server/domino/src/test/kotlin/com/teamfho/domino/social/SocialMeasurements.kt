package com.teamfho.domino.social

import com.google.cloud.firestore.*
import org.mockito.Mockito
import org.mockito.stubbing.Answer
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicLong

/** Counts document operations, including batched reads and query results; emulator-only test utility. */
object SocialMeasurements {
    val counts=ConcurrentHashMap<String,AtomicLong>()
    private val originals=java.util.Collections.synchronizedMap(java.util.IdentityHashMap<Any,Any>())
    fun count(key:String,n:Long=1){counts.computeIfAbsent(key){AtomicLong()}.addAndGet(n)}
    private fun unwrap(value:Any?):Any? {
        if(value==null)return null
        originals[value]?.let{return it}
        if(value is Array<*>) {
            val copy=java.lang.reflect.Array.newInstance(value.javaClass.componentType,value.size)
            value.forEachIndexed{i,v->java.lang.reflect.Array.set(copy,i,unwrap(v))};return copy
        }
        return value
    }
    fun measured(value:Any?):Any? {
        val type=when(value){is Firestore->Firestore::class.java;is Transaction->Transaction::class.java
            is DocumentReference->DocumentReference::class.java;is CollectionReference->CollectionReference::class.java;is Query->Query::class.java;else->return value}
        val proxy=Mockito.mock(type,Answer {call->
            val args=call.rawArguments.map{unwrap(it)}.toTypedArray();val name=call.method.name
            if(name=="get"&&(value is DocumentReference||value is Transaction&&args.firstOrNull() is DocumentReference))count("reads")
            if(name=="getAll")count("reads",(args[0] as Array<*>).size.toLong())
            if(name in setOf("set","create","update","delete")&&(value is Transaction||value is DocumentReference))count("writes")
            if(name=="runTransaction") {
                @Suppress("UNCHECKED_CAST") val callback=args[0] as Transaction.Function<Any?>
                args[0]=Transaction.Function<Any?>{tx->callback.updateCallback(measured(tx) as Transaction)}
            }
            try{
                call.method.trySetAccessible();val result=call.method.invoke(value,*args)
                if(name=="get"&&value is Query){count("queries");val snapshot=(result as com.google.api.core.ApiFuture<*>).get() as QuerySnapshot;count("reads",maxOf(1,snapshot.size()).toLong())}
                measured(result)
            }catch(e:java.lang.reflect.InvocationTargetException){throw e.targetException}
        })
        originals[proxy]=value!!;return proxy
    }
}
