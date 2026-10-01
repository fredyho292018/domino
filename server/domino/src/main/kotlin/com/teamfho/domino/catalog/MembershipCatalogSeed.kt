package com.teamfho.domino.catalog

object MembershipCatalogSeed {
    val featureKeys=listOf("GAME_REVIEW","MOVE_EXPLANATIONS","ADVANCED_STATS","PUZZLES","LESSONS","COACH_GAMES","BOTS","NO_ADS")
    val backendKeys=setOf("PUBLIC_DUEL","PUBLIC_PARTNERS","FOLLOW_PLAYER","FRIENDS","FRIEND_REQUESTS","PARTY_CREATE","PARTY_INVITE",
        "PRIVATE_DUEL","PRIVATE_PARTNERS","CHOOSE_2V2_PARTNER","PARTY_MATCHMAKING","FULL_HISTORY","FULL_REPLAY","ADVANCED_STATS","PREMIUM_THEMES")
    fun canonical():MembershipCatalogPublication {
        val hierarchy=listOf("FREE","GOLD","PLATINUM","DIAMOND")
        val keys=hierarchy+"FRIENDS_AND_FAMILY"
        val order=mapOf("DIAMOND" to 10,"PLATINUM" to 20,"GOLD" to 30,"FRIENDS_AND_FAMILY" to 40,"FREE" to 50)
        val icons=listOf("icon_menu_membership","icon_premium_star","icon_premium_crown","icon_menu_membership","icon_premium_family")
        val gold=setOf("PUZZLES","LESSONS","COACH_GAMES","BOTS","NO_ADS")
        val platinum=gold+"GAME_REVIEW"
        val diamond=platinum+setOf("MOVE_EXPLANATIONS","ADVANCED_STATS")
        val commercial=mapOf("FREE" to emptySet(),"GOLD" to gold,"PLATINUM" to platinum,"DIAMOND" to diamond,"FRIENDS_AND_FAMILY" to diamond)
        val free=setOf("PUBLIC_DUEL","PUBLIC_PARTNERS","FOLLOW_PLAYER","FRIENDS","FRIEND_REQUESTS")
        val goldBackend=free+setOf("PARTY_CREATE","PARTY_INVITE","PRIVATE_DUEL","PRIVATE_PARTNERS","CHOOSE_2V2_PARTNER","PARTY_MATCHMAKING","PREMIUM_THEMES")
        val platinumBackend=goldBackend+setOf("FULL_HISTORY","FULL_REPLAY")
        val diamondBackend=platinumBackend+"ADVANCED_STATS"
        fun target(capabilities:Set<String>,friends:Int,full:Boolean)=MembershipTargetPlan(capabilities,mapOf(
            "FRIENDS_MAX" to MembershipQuota(false,friends),"HISTORY_MAX" to MembershipQuota(full,if(full)null else 10),
            "REPLAY_MAX" to MembershipQuota(full,if(full)null else 3)))
        val target=MembershipTargetPolicy(1,false,mapOf("FREE" to target(free,5,false),"GOLD" to target(goldBackend,100,false),
            "PLATINUM" to target(platinumBackend,100,true),"DIAMOND" to target(diamondBackend,100,true),"FRIENDS_AND_FAMILY" to target(diamondBackend,100,true)))
        val namesEn=listOf("Free","Gold","Platinum","Diamond","Friends & Family")
        val namesEs=listOf("Gratis","Gold","Platinum","Diamond","Amigos y familia")
        val descriptionsEn=listOf("Base access to the club.","Training and social benefits.","Gold benefits plus game review.","Platinum benefits plus move explanations and advanced statistics.","2–5 players, including the owner, with Diamond-equivalent benefits.")
        val descriptionsEs=listOf("Acceso básico al club.","Beneficios de entrenamiento y sociales.","Beneficios Gold y análisis de partidas.","Beneficios Platinum, explicaciones de jugadas y estadísticas avanzadas.","De 2 a 5 jugadores, incluido el titular, con beneficios equivalentes a Diamond.")
        val featureEn=listOf("Game Review","Move Explanations","Advanced Stats","Puzzles","Lessons","Coach Games","Bots","No Ads")
        val featureEs=listOf("Análisis de partidas","Explicaciones de jugadas","Estadísticas avanzadas","Ejercicios","Lecciones","Partidas con entrenador","Bots","Sin anuncios")
        val descEn=listOf("Analysis and coaching, separate from match replay.","Explanations of moves.","Advanced game statistics.","Domino training exercises.","Learning content.","Practice games with a coach.","Games against bots.","Ad-free presentation.")
        val descEs=listOf("Análisis y entrenamiento, independientes de la repetición de partidas.","Explicaciones de las jugadas.","Estadísticas avanzadas de juego.","Ejercicios de dominó.","Contenido de aprendizaje.","Partidas de práctica con entrenador.","Partidas contra bots.","Presentación sin anuncios.")
        val featureIcons=listOf("icon_premium_review","icon_premium_move","icon_menu_stats","icon_nav_puzzles","icon_nav_learn","icon_menu_coach","icon_premium_robot","icon_premium_noads")
        return MembershipCatalogPublication(1,1,"en",listOf("es","en"),hierarchy,
            keys.mapIndexed{i,k->MembershipPlan(k,icons[i],order.getValue(k),true,if(k=="FRIENDS_AND_FAMILY")"MULTI_PLAYER" else "INDIVIDUAL")},
            featureKeys.mapIndexed{i,k->MembershipFeature(k,MembershipFeatureKind.BOOLEAN_CAPABILITY,featureIcons[i],i*10)},
            keys.flatMap{k->featureKeys.map{f->MembershipPlanFeature(k,f,f in commercial.getValue(k))}},
            mapOf("en" to keys.mapIndexed{i,k->k to MembershipPlanTranslation(namesEn[i],descriptionsEn[i])}.toMap(),
                "es" to keys.mapIndexed{i,k->k to MembershipPlanTranslation(namesEs[i],descriptionsEs[i])}.toMap()),
            mapOf("en" to featureKeys.mapIndexed{i,k->k to MembershipFeatureTranslation(featureEn[i],descEn[i])}.toMap(),
                "es" to featureKeys.mapIndexed{i,k->k to MembershipFeatureTranslation(featureEs[i],descEs[i])}.toMap()),
            keys.filter{it!="FREE"}.flatMap{k->MembershipPlatform.entries.flatMap{platform->MembershipBillingPeriod.entries.map{period->MembershipBillingProduct(platform,k,period,null,false)}}},
            target,MembershipTrialReference(1),MembershipFamilyPolicy(),"2026-09-30T00:00:00Z")
    }
    fun run(repository:MembershipCatalogRepository)=repository.publish(canonical())
}
