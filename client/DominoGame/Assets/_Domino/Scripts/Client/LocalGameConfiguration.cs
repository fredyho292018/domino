using System;
using Domino.Configuration;
using UnityEngine;

namespace Domino.Client
{
    public static class LocalGameConfiguration
    {
        public static GameConfigurationSnapshot Load(TextAsset asset)
        {
            if (!asset) throw new ArgumentException("Assign the bundled configuration TextAsset in DominoClient.");
            return Parse(asset.text);
        }
        public static GameConfigurationSnapshot Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                throw new ArgumentException("Expected a JSON configuration object.");
            GameConfigurationDto dto;
            // Preserve absent optional policy objects. Unity's inline serializer materializes
            // them as empty objects, which changes the meaning of the legacy v1 fixture.
            try { dto = Newtonsoft.Json.JsonConvert.DeserializeObject<GameConfigurationDto>(json); }
            catch (Newtonsoft.Json.JsonException error) { throw new ArgumentException("Invalid configuration JSON.", error); }
            return GameConfigurationValidator.Validate(dto);
        }
    }
}
