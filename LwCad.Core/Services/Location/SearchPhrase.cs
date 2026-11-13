using System;
using System.Text;
using System.Text.RegularExpressions;

namespace LwCadSystemUX.Services.Location
{
    /// <summary>
    /// The LaGeo04x search syntax (CheckPhraseVar): a phrase containing % (or *)
    /// is a LIKE pattern, one ending in + means "from here on" (!&lt;), anything else
    /// is an exact match. The legacy appended % to what was typed, so a plain entry
    /// is a starts-with search. Matching is case-insensitive, as the CAD database
    /// collation is.
    ///
    /// The store fetches on <see cref="Prefix"/> - the text before the first
    /// wildcard - and the engine then applies the full phrase with <see cref="Matches"/>,
    /// so both back ends give the same answer.
    /// </summary>
    public sealed class SearchPhrase
    {
        private readonly Regex _like;

        public SearchPhrase(string phrase)
        {
            var text = (phrase ?? string.Empty).Trim().Replace('*', '%').Replace(";", string.Empty).Replace("--", string.Empty);
            if (text == "% or +") text = "+";

            if (text.Contains('%'))
            {
                Mode = PhraseMode.Like;
                Value = text;
                _like = new Regex("^" + ToRegex(text) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            else if (text.EndsWith("+", StringComparison.Ordinal))
            {
                Mode = PhraseMode.From;
                Value = text.TrimEnd('+');
            }
            else
            {
                Mode = PhraseMode.Equals;
                Value = text;
            }

            var cut = Value.IndexOfAny(new[] { '%', '_' });
            Prefix = cut < 0 ? Value : Value.Substring(0, cut);
        }

        public PhraseMode Mode { get; }

        public string Value { get; }

        /// <summary>The literal text a store can index on.</summary>
        public string Prefix { get; }

        public bool Matches(string value)
        {
            var v = (value ?? string.Empty).TrimEnd();

            switch (Mode)
            {
                case PhraseMode.Like:
                    return _like.IsMatch(v);
                case PhraseMode.From:
                    return string.Compare(v, Value, StringComparison.OrdinalIgnoreCase) >= 0;
                default:
                    return string.Equals(v, Value.TrimEnd(), StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>A starts-with phrase on text with its spaces removed, as the legacy builds it.</summary>
        public static SearchPhrase StartsWith(string text) =>
            new SearchPhrase(Squash(text) + "%");

        /// <summary>Upper case with every space removed - how the *_ZIP and SPACELESS columns are stored.</summary>
        public static string Squash(string text) =>
            (text ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();

        private static string ToRegex(string like)
        {
            var sb = new StringBuilder();
            foreach (var c in like)
            {
                if (c == '%') sb.Append(".*");
                else if (c == '_') sb.Append('.');
                else sb.Append(Regex.Escape(c.ToString()));
            }

            return sb.ToString();
        }
    }

    public enum PhraseMode
    {
        Like,
        From,
        Equals
    }
}
