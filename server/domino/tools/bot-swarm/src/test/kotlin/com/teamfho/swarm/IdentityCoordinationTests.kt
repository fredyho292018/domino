package com.teamfho.swarm
import kotlin.test.*
class IdentityCoordinationTests {
    private val nonce=ByteArray(32){it.toByte()}
    private fun proofs()=(0..3).map{IdentityCoordination.proof(nonce,"fixture-$it")}
    private fun compare(p:List<IdentityProof>)=IdentityCoordination.compare(IdentityCoordination.session(nonce),p)
    @Test fun distinct(){assertEquals("YES",compare(proofs())["DISTINCT_MATCH_PLAYERS"])}
    @Test fun unityDuplicate(){val p=proofs().toMutableList();p[1]=p[0];assertEquals("NO",compare(p)["DISTINCT_MATCH_PLAYERS"])}
    @Test fun slotDuplicate(){val p=proofs().toMutableList();p[2]=p[1];assertEquals("NO",compare(p)["DISTINCT_MATCH_PLAYERS"])}
    @Test fun wrongProject(){assertFails{compare(proofs().map{it.copy(project="wrong")})}}
    @Test fun localScope(){assertFails{compare(proofs().map{it.copy(scope="LOCAL")})}}
    @Test fun missing(){assertFails{compare(proofs().dropLast(1))}}
    @Test fun wrongNonce(){val p=proofs().toMutableList();p[0]=IdentityCoordination.proof(ByteArray(32){99},"fixture-0");assertFails{compare(p)}}
    @Test fun malformed(){assertFails{compare(proofs().map{it.copy(fingerprint="malformed!")})}}
}
