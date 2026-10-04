package com.teamfho.domino.player
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import kotlin.test.*

class PlayerProfileEditingTests {
 private val repo=ProgressMemory().also{it.initialize()}
 private val service=PlayerProfileEditingService(repo)
 private val user=FirebaseIdentity("fixture-player",true)
 private fun request(alias:String="Fixture")=PlayerProfileEditRequest(" Ana ","Rivera",alias,"CU","es",0,0)
 @Test fun `atomic five field update preserves all other domains and createdAt`() {
  val before=repo.docs.toMap();val result=service.update(user,request("NewAlias"))
  assertEquals("Ana",result.firstName);assertEquals("Rivera",result.lastName);assertEquals("NewAlias",result.displayName);assertEquals("CU",result.country);assertEquals("es",result.preferredLanguage)
  assertEquals(before["players/fixture-player"]!!["createdAt"],repo.docs["players/fixture-player"]!!["createdAt"])
  for(path in listOf("onboarding","dominoProfile","entitlementState"))assertEquals(before["players/fixture-player/$path/current"],repo.docs["players/fixture-player/$path/current"])
  assertEquals("RELEASED",repo.docs[PlayerAliasReservations.path("Fixture")]!!["state"])
  assertFalse(repo.docs[PlayerAliasReservations.path("Fixture")]!!.containsKey("playerId"))
  assertEquals(user.uid,repo.docs[PlayerAliasReservations.path("NewAlias")]!!["playerId"])
 }
 @Test fun `owner unchanged alias and repeated update are no ops for reservation`() {
  val reservation=repo.docs[PlayerAliasReservations.path("Fixture")];val first=service.update(user,request());val before=repo.docs.toMap()
  assertEquals(first,service.update(user,request()));assertEquals(before,repo.docs);assertEquals(reservation,repo.docs[PlayerAliasReservations.path("Fixture")])
 }
 @Test fun `taken including normalized case rolls back all fields`() {
  repo.docs[PlayerAliasReservations.path("Other")]=mapOf("state" to "CLAIMED","playerId" to "other","normalizationVersion" to 1L)
  for(alias in listOf("Other","OTHER"," Other ")){val before=repo.docs.toMap();assertEquals("DISPLAY_NAME_TAKEN",assertFailsWith<OnboardingFailure>{service.update(user,request(alias))}.code);assertEquals(before,repo.docs)}
 }
 @Test fun `availability before race never bypasses final claim`() {
  assertEquals("AVAILABLE",AliasAvailability.check(user.uid,"Other"){repo.docs[it]}.state)
  repo.docs[PlayerAliasReservations.path("Other")]=mapOf("state" to "CLAIMED","playerId" to "other","normalizationVersion" to 1L)
  val before=repo.docs.toMap();assertEquals("DISPLAY_NAME_TAKEN",assertFailsWith<OnboardingFailure>{service.update(user,request("Other"))}.code);assertEquals(before,repo.docs)
 }
 @Test fun `stale revisions reject overwrites`() {service.update(user,request());val before=repo.docs.toMap();assertEquals("REVISION_MISMATCH",assertFailsWith<OnboardingFailure>{service.update(user,request("Different"))}.code);assertEquals(before,repo.docs)}
 @Test fun `field errors are specific and nonmutating`() {
  for((request,code) in listOf(request().copy(firstName="") to "FIRST_NAME_INVALID",request().copy(lastName="") to "LAST_NAME_INVALID",request().copy(country="XX") to "COUNTRY_INVALID",request().copy(preferredLanguage="zz") to "LANGUAGE_UNSUPPORTED")){val before=repo.docs.toMap();assertEquals(code,assertFailsWith<OnboardingFailure>{service.update(user,request)}.code);assertEquals(before,repo.docs)}
 }
 @Test fun `completed and absent onboarding are irrelevant and auth provider independent`() {
  repo.docs.remove("players/fixture-player/onboarding/current")
  val result=service.update(FirebaseIdentity(user.uid,false),request());assertEquals("Ana",result.firstName);assertFalse(repo.docs.containsKey("players/fixture-player/onboarding/current"))
 }
 @Test fun `read only get never creates missing player`() {val before=repo.docs.toMap();service.get(user);assertEquals(before,repo.docs);assertEquals("PLAYER_NOT_FOUND",assertFailsWith<OnboardingFailure>{service.get(FirebaseIdentity("absent",true))}.code);assertEquals(before,repo.docs)}
}
