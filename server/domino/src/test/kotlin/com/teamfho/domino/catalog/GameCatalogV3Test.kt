package com.teamfho.domino.catalog
import kotlin.test.*
import org.junit.jupiter.api.Test
class GameCatalogV3Test {
 @Test fun `v3 only adds online execution to duel while preserving all rules`() {
  val old=GameCatalogV2Publisher.canonical();val next=GameCatalogV3Publisher.canonical()
  assertEquals(3,next.catalogVersion)
  assertEquals(old,next.copy(catalogVersion=2,modes=next.modes.map {if(it.key=="DUEL_1V1")it.copy(executionModesSupported=listOf(ExecutionMode.LOCAL))else it}))
  val resolved=GameCatalogValidator.resolve(next)
  assertEquals(listOf(ExecutionMode.LOCAL,ExecutionMode.ONLINE),resolved.modes.single {it.key=="DUEL_1V1"}.executionModesSupported)
 }
}
