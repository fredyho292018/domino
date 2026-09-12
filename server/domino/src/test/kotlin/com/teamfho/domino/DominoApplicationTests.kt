package com.teamfho.domino

import org.junit.jupiter.api.Test
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.test.context.ActiveProfiles
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.context.ApplicationContext
import com.google.firebase.FirebaseApp
import com.google.firebase.auth.FirebaseAuth
import com.google.cloud.firestore.Firestore
import kotlin.test.assertTrue
import org.springframework.context.annotation.Import
import com.teamfho.domino.security.FakeAuthConfiguration

@SpringBootTest
@ActiveProfiles("test")
@Import(FakeAuthConfiguration::class, com.teamfho.domino.player.FakePlayerFoundationConfiguration::class)
class DominoApplicationTests {

	@Autowired
	lateinit var context: ApplicationContext

	@Test
	fun contextLoads() {
		assertTrue(context.getBeansOfType(FirebaseApp::class.java).isEmpty())
		assertTrue(context.getBeansOfType(FirebaseAuth::class.java).isEmpty())
		assertTrue(context.getBeansOfType(Firestore::class.java).isEmpty())
	}

}
