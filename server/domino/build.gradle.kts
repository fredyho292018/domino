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
	compilerOptions {
		freeCompilerArgs.addAll("-Xjsr305=strict", "-Xannotation-default-target=param-property")
	}
}

tasks.withType<Test> {
	useJUnitPlatform()
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
