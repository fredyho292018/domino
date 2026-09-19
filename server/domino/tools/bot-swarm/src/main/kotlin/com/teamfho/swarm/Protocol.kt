package com.teamfho.swarm

import tools.jackson.databind.JsonNode
import tools.jackson.databind.json.JsonMapper
import java.util.UUID

object Json { val mapper=JsonMapper.builder().build(); fun read(s:String):JsonNode=mapper.readTree(s); fun write(v:Any)=mapper.writeValueAsString(v) }
fun JsonNode.text(key:String)=path(key).asString("")
fun JsonNode.number(key:String)=path(key).asInt()
enum class ClientState { STARTING, AUTHENTICATING, CONNECTING, IDLE, JOINING_QUEUE, SEARCHING, MATCH_FOUND, ENTERING_MATCH, PLAYING, MATCH_FINISHED, REQUEUE_DELAY, RECONNECTING, FAILED, STOPPED }

data class Mode(val key:String,val requiredPlayers:Int,val ruleSetId:String,val ruleSetVersion:Int,val teams:List<List<Int>>,val rules:JsonNode) {
    fun team(seat:Int):Int?=teams.indexOfFirst{seat in it}.takeIf{it>=0}
    companion object {
        fun read(n:JsonNode):Mode {
            require(n.path("active").asBoolean()&&n.path("executionModesSupported").any{it.asString()=="ONLINE"}){"MODE_NOT_ONLINE"}
            val count=n.number("playerCount");require(count in 2..20&&!n.path("botsAllowed").asBoolean()){"INVALID_REMOTE_TOPOLOGY"}
            val teams=n.path("seatTeams").toList().map{t->t.toList().map{it.asInt()}}
            require(teams.isEmpty()||teams.flatten().sorted()==(0 until count).toList()){"INVALID_TEAMS"}
            val rules=n.path("ruleSet");require(rules.isObject&&rules.number("version")>0&&rules.text("id")==n.text("defaultRuleSetId")){"INVALID_RULESET"}
            return Mode(n.text("key"),count,rules.text("id"),rules.number("version"),teams,rules)
        }
        fun catalog(root:JsonNode,key:String):Mode {
            require(root.number("catalogSchemaVersion")==1){"UNSUPPORTED_CATALOG"}
            return read(root.path("modes").single{it.text("key")==key})
        }
    }
}

// Only the server-authorized projection enters this class. No access to repositories or other clients.
class Projection(val raw:JsonNode) {
    val public=raw.path("publicState")
    val seat=raw.path("privateState").number("seat")
    val id=public.text("matchId")
    val sequence=raw.path("lastSequence").asLong()
    val phase=raw.text("phase")
    val mode=Mode.read(Json.read(raw.path("ruleSnapshot").text("effectiveModeJson")))
    val hand=raw.path("privateState").path("hand")
    init {
        require(id.matches(Regex("[A-Za-z0-9_-]{1,128}"))&&seat in 0 until mode.requiredPlayers){"INVALID_PROJECTION"}
        require(sequence>=0&&sequence==public.path("lastSequence").asLong()&&sequence==raw.path("privateState").path("lastSequence").asLong()){"INVALID_SEQUENCE"}
        require(hand.isArray&&hand.size()<=mode.rules.number("tilesPerPlayer")){"INVALID_HAND"}
        require(!public.has("hands")&&!public.has("hand")&&!raw.has("hands")&&!raw.path("privateState").has("hands")){"PRIVATE_HAND_LEAK"}
        require(public.path("participants").size()==mode.requiredPlayers&&public.path("participants").all{it.text("controlType")=="REMOTE_HUMAN"}){"INVALID_PARTICIPANTS"}
    }
    fun intent():Map<String,Any>? = when(phase) {
        "STARTER_SELECTION" -> {
            val st=raw.path("starter")
            when(st.text("method")) {
                "HIGH_TILE_SELECTION" -> if(st.path("selectedSeats").any{it.asInt()==seat})null else (st.path("availableCandidates").firstOrNull{it.asInt()==seat}?:st.path("availableCandidates").firstOrNull())?.let{mapOf("type" to "SELECT_STARTER_TILE","candidate" to it.asInt())}
                "EVEN_ODD_GUESS" -> if(st.number("guessingSeat")==seat)mapOf("type" to "SUBMIT_EVEN_ODD_GUESS","even" to true)else null
                else -> error("UNSUPPORTED_STARTER")
            }
        }
        "PLAYING" -> if(public.number("currentSeat")!=seat)null else firstMove()
        // One participant advances a round; avoids simultaneous NEXT_ROUND commands.
        "ROUND_FINISHED" -> if(seat==0)mapOf("type" to "NEXT_ROUND")else null
        else -> null
    }
    private fun firstMove():Map<String,Any>? {
        val board=public.path("board")
        for(tile in hand)for(end in listOf("LEFT","RIGHT")) {
            val pip=if(board.isEmpty)-1 else if(end=="LEFT")board.first().path("tile").number("sideA")else board.last().path("tile").number("sideB")
            if(pip<0||tile.number("sideA")==pip||tile.number("sideB")==pip)return mapOf("type" to "PLAY_TILE","tile" to mapOf("sideA" to tile.number("sideA"),"sideB" to tile.number("sideB")),"chainEnd" to end)
        }
        // Current protocol requires explicit PASS in both modes; no server auto-pass policy is published.
        return mapOf("type" to "PASS")
    }
}

class SequenceTracker {
    var current:Projection?=null;private set
    var gaps=0;private set
    fun snapshot(next:Projection) {
        current?.let{require(it.id==next.id&&it.seat==next.seat){"ASSIGNMENT_CHANGED"};if(next.sequence<it.sequence)return}
        current=next
    }
    fun update(payload:JsonNode):Boolean {
        val old=current?:return false
        if(payload.text("matchId")!=old.id)return false
        val next=Projection(payload.path("snapshot"))
        if(next.sequence<=old.sequence)return false
        var expected=old.sequence+1
        if(payload.path("firstSequence").asLong()!=expected){gaps++;return false}
        for(e in payload.path("events")) {
            if(e.path("sequence").asLong()!=expected++){gaps++;return false}
            val event=e.path("event")
            require(event.isMissingNode||event.isNull||event.text("visibility")!="PLAYER_PRIVATE"||event.number("targetSeat")==old.seat){"PRIVATE_EVENT_LEAK"}
        }
        if(expected-1!=next.sequence){gaps++;return false}
        snapshot(next);return true
    }
    fun clear(){current=null}
}
class LogicalCommand(val body:Map<String,Any>) {
    companion object { fun create(snapshot:Projection,intent:Map<String,Any>)=LogicalCommand(intent+mapOf("protocolVersion" to 1,"commandId" to UUID.randomUUID().toString(),"matchId" to snapshot.id)) }
    val id get()=body.getValue("commandId") as String
}
