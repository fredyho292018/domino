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
            var dto = new GameConfigurationDto();
            JsonUtility.FromJsonOverwrite(json, dto);
            return GameConfigurationValidator.Validate(dto);
        }
    }
}
