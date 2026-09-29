package com.teamfho.swarm
import kotlinx.coroutines.*
import kotlinx.coroutines.channels.Channel
import kotlin.test.*
class S721ConnectOnlyTest {
    class Fake(val fail:Boolean=false):S721Port {
        var bootstrapCalls=0;var closed=0;val messages=Channel<String>(10)
        override suspend fun auth(){}
        override suspend fun bootstrap(){bootstrapCalls++;if(fail)throw java.net.http.HttpTimeoutException("fixture")}
        override suspend fun connect(){messages.send("AUTHENTICATED")}
        override suspend fun ping(){messages.send("PONG")}
        override suspend fun receive()=messages.receive()
        override suspend fun close(){closed++}
    }
    @Test fun clientSevenBootstrapFailureDoesNotCancelNineteen()=runBlocking {
        val ports=(1..20).map{Fake(it==7)}
        val clients=ports.mapIndexed{i,p->S721Client(i+1,p,{},1,5,1000)}
        val job=launch{s721Isolated(clients.map{c->{c.run()}},{error("unexpected emergency")})}
        withTimeout(5000){while(clients.count{it.stable}!=19 || !clients[6].terminal)delay(5)}
        assertEquals(2,ports[6].bootstrapCalls);assertEquals(19,clients.count{it.active})
        assertTrue(job.isActive);job.cancelAndJoin();assertEquals(0,clients.count{it.active})
    }
    @Test fun globalStopCancelsAllWithoutRetries()=runBlocking {
        val events=mutableListOf<Map<String,Any>>();val ports=(1..20).map{Fake()}
        val clients=ports.mapIndexed{i,p->S721Client(i+1,p,{events.add(it)},1,5,1000)}
        val job=launch{s721Isolated(clients.map{c->{c.run()}},{})}
        withTimeout(5000){while(clients.count{it.stable}!=20)delay(5)}
        job.cancelAndJoin();assertEquals(20,events.count{it["state"]=="STOPPED"})
        assertTrue(clients.all{it.attempts==1&&!it.active});assertTrue(ports.all{it.closed==1})
    }
    @Test fun classificationsAreBoundedAndPhaseSpecific() {
        assertEquals("BOOTSTRAP_TIMEOUT",s721Category(java.net.http.HttpTimeoutException("fixture"),"BOOTSTRAP"))
        assertEquals("DNS",s721Category(java.net.UnknownHostException(),"BOOTSTRAP"))
        assertEquals("TLS",s721Category(javax.net.ssl.SSLException("fixture"),"WS_CONNECT"))
        assertEquals("SERVER_CLOSE",s721Category(SafeFailure("WS_CLOSED_1008"),"WS_AUTH"))
        assertEquals("BOOTSTRAP_HTTP",s721Category(SafeFailure("HTTP_502"),"BOOTSTRAP"))
    }
}
