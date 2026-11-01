using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.Net.Messages;

namespace Lazpoly.App.Save
{
    /// <summary>
    /// Binary save format: a magic number and format version, then the generators, the AI seats and
    /// the game state written the same way the network writes it. Files of the previous version
    /// (before table rules and the Faz 7c squares) still open; they get the default table.
    /// </summary>
    public static class SavedGameCodec
    {
        public const int FormatVersion = 4;
        public const int OldestReadableVersion = 1;
        private const ulong Magic = 0x4C415A504F4C5953UL; // "LAZPOLYS"

        public static byte[] Encode(SavedGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.Snapshot == null) throw new ArgumentException("Save has no game state.", nameof(save));

            var w = new NetWriter();
            w.WriteULong(Magic);
            w.WriteInt(FormatVersion);
            w.WriteULong(save.BoardChecksum);
            w.WriteULong(save.DiceState);
            w.WriteULong(save.DiceIncrement);
            w.WriteULong(save.AiState);
            w.WriteULong(save.AiIncrement);

            var ids = new List<int>(save.AiPlayers.Keys);
            ids.Sort();
            w.WriteInt(ids.Count);
            foreach (int id in ids)
            {
                w.WriteInt(id);
                w.WriteByte((byte)save.AiPlayers[id].Difficulty);
                w.WriteInt(save.AiPlayers[id].LastProposalTurn);
            }

            MessageCodec.WriteTable(w, save.Table ?? Core.Rules.TableRules.Default);
            MessageCodec.WriteSnapshot(w, save.Snapshot);
            return w.ToArray();
        }

        /// <summary>Save format 1 wrote the legacy snapshot, 2 the table-rules one, 3 the building-visit one, 4 the current one.</summary>
        private static int SnapshotVersionOf(int saveVersion) =>
            saveVersion >= 4 ? MessageCodec.SnapshotVersion
            : saveVersion == 3 ? MessageCodec.VisitSnapshotVersion
            : saveVersion == 2 ? MessageCodec.TableSnapshotVersion
            : MessageCodec.LegacySnapshotVersion;

        /// <summary>Reads a save of this or an older readable version. Throws <see cref="FormatException"/> for anything else.</summary>
        public static SavedGame Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var r = new NetReader(data);
            if (r.ReadULong() != Magic) throw new FormatException("Not a Lazpoly save file.");
            int version = r.ReadInt();
            if (version < OldestReadableVersion || version > FormatVersion) throw new FormatException($"Save format {version} is not supported.");

            var save = new SavedGame
            {
                BoardChecksum = r.ReadULong(),
                DiceState = r.ReadULong(),
                DiceIncrement = r.ReadULong(),
                AiState = r.ReadULong(),
                AiIncrement = r.ReadULong(),
            };
            if ((save.DiceIncrement & 1UL) == 0UL || (save.AiIncrement & 1UL) == 0UL) throw new FormatException("Invalid generator state.");

            int count = r.ReadCount();
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt();
                byte difficulty = r.ReadByte();
                int lastProposalTurn = r.ReadInt();
                if (!Enum.IsDefined(typeof(AiDifficulty), (int)difficulty)) throw new FormatException($"Invalid AI difficulty {difficulty}.");
                if (save.AiPlayers.ContainsKey(id)) throw new FormatException($"Player {id} listed twice.");
                save.AiPlayers.Add(id, new SavedAiPlayer { Difficulty = (AiDifficulty)difficulty, LastProposalTurn = lastProposalTurn });
            }

            save.Table = version >= 2 ? MessageCodec.ReadTable(r) : Core.Rules.TableRules.Default;
            save.Snapshot = MessageCodec.ReadSnapshot(r, SnapshotVersionOf(version));
            if (!r.AtEnd) throw new FormatException("Unexpected data after the save.");
            return save;
        }
    }
}
