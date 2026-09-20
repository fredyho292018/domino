plugins {
	kotlin("jvm") version "2.2.21"
	kotlin("plugin.spring") version "2.2.21"
	id("org.springframework.boot") version "4.0.8"
	id("io.spring.dependency-management") version "1.1.7"
}

group = "com.teamfho"
version = "0.0.1-SNAPSHOT"

springBoot { mainClass.set("com.teamfho.domino.DominoApplicationKt") }

java {
	toolchain {
		languageVersion = JavaLanguageVersion.of(21)
	}
}

repositories {
	mavenCentral()
}

dependencies {
	implementation("com.google.firebase:firebase-admin:9.10.0")
	implementation("org.springframework.boot:spring-boot-starter-actuator")
	implementation("org.springframework.boot:spring-boot-starter-security")
	implementation("org.springframework.boot:spring-boot-starter-validation")
	implementation("org.springframework.boot:spring-boot-starter-webmvc")
	implementation("org.springframework.boot:spring-boot-starter-websocket")
	implementation("org.springframework.boot:spring-boot-starter-data-redis")
	implementation("org.jetbrains.kotlin:kotlin-reflect")
	implementation("tools.jackson.module:jackson-module-kotlin")
	testImplementation("org.springframework.boot:spring-boot-starter-actuator-test")
	testImplementation("org.springframework.boot:spring-boot-starter-security-test")
	testImplementation("org.springframework.boot:spring-boot-starter-validation-test")
	testImplementation("org.springframework.boot:spring-boot-starter-webmvc-test")
	testImplementation("org.jetbrains.kotlin:kotlin-test-junit5")
	testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

kotlin {

    sourceSets.main { kotlin.srcDir("validation-common") }
	compilerOptions {
		freeCompilerArgs.addAll("-Xjsr305=strict", "-Xannotation-default-target=param-property")
	}
}

tasks.withType<Test> {
	useJUnitPlatform()
}

tasks.test {
    useJUnitPlatform { excludeTags("REAL_FIRESTORE", "EMULATOR") }
    // Defense in depth: an accidentally constructed SDK client cannot reach a real project.
    environment("FIRESTORE_EMULATOR_HOST", "127.0.0.1:1")
    environment("GOOGLE_CLOUD_PROJECT", "demo-domino-unit")
}

tasks.register<Test>("realFirestoreTest") {
    description = "Explicit opt-in real Firestore tests; never part of test/check."
    testClassesDirs = sourceSets.test.get().output.classesDirs
    classpath = sourceSets.test.get().runtimeClasspath
    useJUnitPlatform { includeTags("REAL_FIRESTORE") }
    doFirst {
        check(System.getenv("DOMINO_REAL_FIRESTORE_TESTS") == "true") { "REAL_FIRESTORE_DISABLED" }
        check(!System.getenv("FIREBASE_PROJECT_ID").isNullOrBlank()) { "EXPLICIT_PROJECT_REQUIRED" }
    }
}

tasks.register<Test>("emulatorTest") {
    description = "Local Firestore emulator only; no ADC, no real project."
    testClassesDirs = sourceSets.test.get().output.classesDirs
    classpath = sourceSets.test.get().runtimeClasspath
    useJUnitPlatform { includeTags("EMULATOR") }
    environment("FIRESTORE_EMULATOR_HOST", "127.0.0.1:18085")
    environment("GOOGLE_CLOUD_PROJECT", "demo-domino-f0")
}

tasks.register<JavaExec>("swarmEmulatorBackend") {
    description = "Loopback-only I3.1 backend on test classpath; no production verifier bypass."
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.SwarmEmulatorBackend")
}

tasks.register<JavaExec>("inspectSwarmEmulator") {
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.SwarmEmulatorInspection")
}

tasks.register<JavaExec>("replayValidationServer") {
    description = "Explicit test-only loopback replay host using retained archives; no Firestore client."
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.match.ReplayValidationServer")
}

tasks.register<JavaExec>("entitlementValidationServer") {
    description = "P0.1 exact-loopback in-memory validation host; test classpath only."
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.entitlement.EntitlementValidationServer")
}

tasks.register<JavaExec>("socialValidationServer") {
    description = "S1.1 test-classpath loopback host; in-memory only, no Firebase or Firestore."
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.social.SocialValidationServer")
}

tasks.register<JavaExec>("seedMonetizationPolicy") {
    group = "application"
    description = "Explicitly create systemConfig/monetization only if absent; never overwrites. Uses fallback ENV/YAML and ADC."
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.economy.reward.MonetizationPolicySeed")
    args("--seed-if-absent")
}

tasks.register<JavaExec>("seedGameCatalog") {
    group = "application"
    description = "Explicit create-only Game Catalog v1 publication using ADC; verifies existing content."
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.catalog.GameCatalogSeed")
    args("--seed-if-absent")
}

tasks.register<JavaExec>("publishGameCatalogV2") {
    group = "application"
    description = "Explicit immutable catalog v2 publication and rollback verification using ADC."
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.catalog.GameCatalogV2Publisher")
    args("--publish-v2", "--verify-rollback")
}

tasks.register<JavaExec>("validateMatchM4Firestore") {
    group = "verification"
    description = "Explicit isolated M4 match/event/history validation using ADC; no wallet writes."
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.match.MatchFirestoreValidation")
    args("--validate-m4")
    providers.gradleProperty("m4MatchId").orNull?.let { args("--inspect-match=$it") }
}

tasks.register<JavaExec>("exportOnlineFixtures") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.OnlineFixtureExporter")
}

tasks.register<JavaExec>("validateOnlineI1Real") {
    group = "verification"
    description = "Explicit real Firebase + local WebSocket + synthetic Firestore Match validation; no wallet calls."
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.OnlineRealValidation")
    args("--validate-i1")
}

tasks.register<JavaExec>("validateOnlineUnityI11") {
    group = "verification"
    description = "Explicit JVM companion and local server for real Unity I1.1 validation."
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.OnlineUnityValidation")
    args("--validate-i11")
}

tasks.register<JavaExec>("validateOnlineTurnI2") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.OnlineTurnRealValidation")
    args("--validate-i2")
}

tasks.register<JavaExec>("publishGameCatalogV3") {
    group = "application"
    description = "Explicit immutable v3 publication advertising DUEL local and online; preserves v2."
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.catalog.GameCatalogV3Publisher")
    args("--publish-v3")
}

tasks.register<JavaExec>("publishGameCatalogV4") {
    group = "application"
    description = "Explicit immutable v4 publication adding four-human partners with the unchanged shared v1 RuleSet."
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.catalog.GameCatalogV4Publisher")
    args("--publish-v4")
}

tasks.register<JavaExec>("exportPartnersFixtures") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.PartnersFixtureExporter")
}

tasks.register<JavaExec>("inspectPartnersMatch") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.PartnersRealInspection")
    providers.gradleProperty("matchId").orNull?.let { args(it) }
}

tasks.register<JavaExec>("inspectTwoUnityMatch") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("com.teamfho.domino.online.TwoUnityMatchInspection")
    providers.gradleProperty("matchId").orNull?.let { args(it) }
}
