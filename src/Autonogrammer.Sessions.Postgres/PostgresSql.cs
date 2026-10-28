using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>Small shared helpers for building PostgreSQL commands and advisory-lock keys.</summary>
internal static class PostgresSql
{
    /// <summary>Creates a command on <paramref name="connection"/> that runs inside <paramref name="transaction"/> when one is given.</summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="transaction">The transaction to enlist in, or <see langword="null"/> for autocommit.</param>
    /// <returns>The command, with no text set. The caller assigns a compile-time constant to <see cref="NpgsqlCommand.CommandText"/> and disposes the command.</returns>
    internal static NpgsqlCommand Create(NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        return command;
    }

    /// <summary>
    /// Derives a stable 64-bit advisory-lock key from a string: the first eight bytes of its SHA-256 digest.
    /// Unlike <c>string.GetHashCode</c>, the value is the same in every process and every run.
    /// </summary>
    /// <param name="value">The text to key on.</param>
    /// <returns>The advisory-lock key.</returns>
    internal static long AdvisoryKey(string value)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BinaryPrimitives.ReadInt64LittleEndian(digest);
    }
}
