using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GreatVoyage.Application;
using GreatVoyage.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GreatVoyage.Infrastructure
{
    public sealed class JsonFileSessionStore : ISessionStore
    {
        private const int Version = 1;
        private readonly string path;
        private readonly JsonSerializerSettings settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 32,
            MissingMemberHandling = MissingMemberHandling.Error
        };

        public string FilePath => path;
        public bool RecoveredBackup { get; private set; }
        public string LastError { get; private set; } = "";

        public JsonFileSessionStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is required.");
            path = Path.Combine(Path.GetFullPath(directory), "session.json");
            settings.Converters.Add(new PositionConverter());
        }

        public GameSessionState Load(Func<GameSessionState, bool> placementValid = null)
        {
            RecoveredBackup = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            Exception first = null;
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    if (new FileInfo(candidate).Length > 1024 * 1024) throw new InvalidDataException("Save is too large.");
                    var envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(candidate), settings);
                    if (envelope == null || envelope.Version != Version || string.IsNullOrEmpty(envelope.Payload)
                        || !string.Equals(Hash(envelope.Payload), envelope.Sha256, StringComparison.Ordinal))
                        throw new InvalidDataException("Save version or checksum is invalid.");
                    var state = JsonConvert.DeserializeObject<GameSessionState>(envelope.Payload, settings);
                    if (state == null || state.Player == null || state.Clock == null
                        || (placementValid != null && !placementValid(state)))
                        throw new InvalidDataException("Invalid player placement.");
                    RecoveredBackup = candidate.EndsWith(".bak", StringComparison.Ordinal);
                    return state;
                }
                catch (Exception ex) when (ex is IOException || ex is JsonException || ex is ArgumentException)
                {
                    if (first == null) first = ex;
                }
            }
            throw new InvalidDataException("No valid save could be restored. Files were preserved.", first);
        }

        public bool TrySave(GameSessionState state, out string error)
        {
            var temporary = path + ".tmp";
            try
            {
                var payload = JsonConvert.SerializeObject(state, Formatting.None, settings);
                var envelope = new Envelope { Version = Version, Payload = payload, Sha256 = Hash(payload) };
                var bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(envelope, settings));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, RecoveredBackup ? null : path + ".bak");
                else File.Move(temporary, path);
                RecoveredBackup = false;
                LastError = error = "";
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                LastError = error = ex.Message;
                return false;
            }
        }

        private static string Hash(string text)
        {
            using (var algorithm = SHA256.Create())
                return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        private sealed class Envelope
        {
            public int Version { get; set; }
            public string Payload { get; set; }
            public string Sha256 { get; set; }
        }

        private sealed class PositionConverter : JsonConverter<WorldPosition>
        {
            public override void WriteJson(JsonWriter writer, WorldPosition value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("AreaId"); writer.WriteValue(value.AreaId);
                writer.WritePropertyName("X"); writer.WriteValue(value.X);
                writer.WritePropertyName("Y"); writer.WriteValue(value.Y);
                writer.WriteEndObject();
            }

            public override WorldPosition ReadJson(JsonReader reader, Type objectType, WorldPosition existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                var value = JObject.Load(reader);
                if (value["AreaId"] == null || value["X"] == null || value["Y"] == null)
                    throw new JsonSerializationException("Incomplete position.");
                return new WorldPosition((string)value["AreaId"], (float)value["X"], (float)value["Y"]);
            }
        }
    }
}
