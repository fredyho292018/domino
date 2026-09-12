package com.teamfho.domino.realtime

import org.springframework.boot.health.actuate.endpoint.HealthEndpointGroup
import org.springframework.boot.health.actuate.endpoint.HealthEndpointGroups
import org.springframework.boot.health.actuate.endpoint.HealthEndpointGroupsPostProcessor
import org.springframework.stereotype.Component

// Redis retains its real UP/DOWN result in the realtime group. It is not a REST dependency.
@Component
class RealtimeHealthGroups : HealthEndpointGroupsPostProcessor {
    override fun postProcessHealthEndpointGroups(groups: HealthEndpointGroups): HealthEndpointGroups {
        val primary = groups.primary
        return object : HealthEndpointGroups by groups {
            override fun getPrimary(): HealthEndpointGroup = object : HealthEndpointGroup by primary {
                override fun isMember(name: String) = name != "redis" && primary.isMember(name)
            }
        }
    }
}
