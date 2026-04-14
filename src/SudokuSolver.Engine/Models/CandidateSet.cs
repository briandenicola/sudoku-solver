using System.Collections;
using System.Numerics;
using System.Text;

namespace SudokuSolver.Engine.Models;

/// <summary>
/// A compact set of candidate digits (1-9) stored as a bitmask.
/// Bit 0 = digit 1, bit 1 = digit 2, ..., bit 8 = digit 9.
/// </summary>
public readonly struct CandidateSet : IEquatable<CandidateSet>, IEnumerable<int>
{
    private readonly ushort _bits;

    private CandidateSet(ushort bits) => _bits = bits;

    public static CandidateSet Empty => new(0);
    public static CandidateSet All => new(0b_1_1111_1111); // bits 0-8

    public static CandidateSet Of(params int[] digits)
    {
        ushort bits = 0;
        foreach (var d in digits)
        {
            ValidateDigit(d);
            bits |= (ushort)(1 << (d - 1));
        }
        return new CandidateSet(bits);
    }

    public int Count => BitOperations.PopCount(_bits);
    public bool IsEmpty => _bits == 0;
    public bool Contains(int digit) => (_bits & (1 << (digit - 1))) != 0;

    public CandidateSet Add(int digit)
    {
        ValidateDigit(digit);
        return new CandidateSet((ushort)(_bits | (1 << (digit - 1))));
    }

    public CandidateSet Remove(int digit)
    {
        ValidateDigit(digit);
        return new CandidateSet((ushort)(_bits & ~(1 << (digit - 1))));
    }

    public CandidateSet Union(CandidateSet other) => new((ushort)(_bits | other._bits));
    public CandidateSet Intersect(CandidateSet other) => new((ushort)(_bits & other._bits));
    public CandidateSet Except(CandidateSet other) => new((ushort)(_bits & ~other._bits));
    public bool IsSubsetOf(CandidateSet other) => (_bits & ~other._bits) == 0;
    public bool Overlaps(CandidateSet other) => (_bits & other._bits) != 0;

    /// <summary>Returns the single digit if count is 1, otherwise throws.</summary>
    public int Single()
    {
        if (Count != 1)
            throw new InvalidOperationException($"CandidateSet has {Count} elements, expected 1.");
        return BitOperations.TrailingZeroCount(_bits) + 1;
    }

    public IEnumerator<int> GetEnumerator()
    {
        var remaining = _bits;
        while (remaining != 0)
        {
            var bit = remaining & (ushort)(-(short)remaining); // lowest set bit
            yield return BitOperations.TrailingZeroCount(bit) + 1;
            remaining &= (ushort)~bit;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(CandidateSet other) => _bits == other._bits;
    public override bool Equals(object? obj) => obj is CandidateSet other && Equals(other);
    public override int GetHashCode() => _bits;
    public static bool operator ==(CandidateSet left, CandidateSet right) => left.Equals(right);
    public static bool operator !=(CandidateSet left, CandidateSet right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsEmpty) return "{}";
        var sb = new StringBuilder("{");
        foreach (var d in this)
        {
            if (sb.Length > 1) sb.Append(',');
            sb.Append(d);
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static void ValidateDigit(int digit)
    {
        if (digit is < 1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(digit), digit, "Digit must be between 1 and 9.");
    }
}
