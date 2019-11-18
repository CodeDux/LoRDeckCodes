using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoRDeckCodes
{
    public class DeckEncoder
    {
        private readonly static int CARD_CODE_LENGTH = 7;
        private static Dictionary<string, int> FactionCodeToIntIdentifier = new Dictionary<string, int>();
        private static Dictionary<int, string> IntIdentifierToFactionCode = new Dictionary<int, string>();
        private static string[] Factions = {"DE", "FR", "IO", "NX", "PZ", "SI"};
        private readonly static int MAX_KNOWN_VERSION = 1;

        static DeckEncoder()
        {
            FactionCodeToIntIdentifier.Add("DE", 0);
            FactionCodeToIntIdentifier.Add("FR", 1);
            FactionCodeToIntIdentifier.Add("IO", 2);
            FactionCodeToIntIdentifier.Add("NX", 3);
            FactionCodeToIntIdentifier.Add("PZ", 4);
            FactionCodeToIntIdentifier.Add("SI", 5);
            IntIdentifierToFactionCode.Add(0, "DE");
            IntIdentifierToFactionCode.Add(1, "FR");
            IntIdentifierToFactionCode.Add(2, "IO");
            IntIdentifierToFactionCode.Add(3, "NX");
            IntIdentifierToFactionCode.Add(4, "PZ");
            IntIdentifierToFactionCode.Add(5, "SI");
        }

        public static List<CardCodeAndCount> GetDeckFromCode(string code)
        {
            var codeSpan = code.AsSpan();
            var length = Base32Span.Prepare(in codeSpan);
            Span<byte> bytes = stackalloc byte[length];

            try
            {
                Base32Span.Decode(in codeSpan, ref bytes);
            }
            catch
            {
                throw new ArgumentException("Invalid deck code");
            }

            //grab format and version
            //var format = bytes[0] >> 4;
            var version = bytes[0] & 0xF;

            if (version > MAX_KNOWN_VERSION)
            {
                throw new ArgumentException(
                    "The provided code requires a higher version of this library; please update.");
            }

            bytes = bytes.Slice(1);
            var result = new List<CardCodeAndCount>(64);

            for (var i = 3; i > 0; i--)
            {
                var numGroupOfs = Varint.PopVarint(ref bytes);

                for (var j = 0; j < numGroupOfs; j++)
                {
                    var numOfsInThisGroup = Varint.PopVarint(ref bytes);
                    var set = Varint.PopVarint(ref bytes);
                    var faction = Varint.PopVarint(ref bytes);
                    var setFactionString = set.ToString().PadLeft(2, '0') + Factions[faction];

                    for (var k = 0; k < numOfsInThisGroup; k++)
                    {
                        var card = Varint.PopVarint(ref bytes);

                        var newEntry = new CardCodeAndCount()
                        {
                            CardCode = setFactionString + card.ToString().PadLeft(3, '0'),
                            Count = i
                        };
                        result.Add(newEntry);
                    }
                }
            }

            //the remainder of the deck code is comprised of entries for cards with counts >= 4
            //this will only happen in Limited and special game modes.
            //the encoding is simply [count] [cardcode]
            while (bytes.Length > 0)
            {
                var fourPlusCount = Varint.PopVarint(ref bytes);
                var fourPlusSet = Varint.PopVarint(ref bytes);
                var fourPlusFaction = Varint.PopVarint(ref bytes);
                var fourPlusNumber = Varint.PopVarint(ref bytes);

                var fourPlusSetString = fourPlusSet.ToString().PadLeft(2, '0');
                var fourPlusNumberString = fourPlusNumber.ToString().PadLeft(3, '0');

                var newEntry = new CardCodeAndCount()
                {
                    CardCode = fourPlusSetString + Factions[fourPlusFaction] + fourPlusNumberString,
                    Count = fourPlusCount
                };
                result.Add(newEntry);
            }

            return result;
        }

        public static string GetCodeFromDeck(List<CardCodeAndCount> deck)
        {
            var bytes = GetDeckCodeBytes(deck);
            return Base32Span.EncodeNoAlloc(bytes);
        }

        private static byte[] GetDeckCodeBytes(List<CardCodeAndCount> deck)
        {
            if (!ValidCardCodesAndCounts(deck))
                throw new ArgumentException("The provided deck contains invalid card codes.");

            const byte formatAndVersion = 17; //i.e. 00010001

            var result = new List<byte>();
            result.Add(formatAndVersion);

            var of3 = new List<CardCodeAndCount>();
            var of2 = new List<CardCodeAndCount>();
            var of1 = new List<CardCodeAndCount>();
            var ofN = new List<CardCodeAndCount>();

            foreach (var ccc in deck)
            {
                if (ccc.Count == 3)
                    of3.Add(ccc);
                else if (ccc.Count == 2)
                    of2.Add(ccc);
                else if (ccc.Count == 1)
                    of1.Add(ccc);
                else if (ccc.Count < 1)
                    throw new ArgumentException("Invalid count of " + ccc.Count + " for card " + ccc.CardCode);
                else
                    ofN.Add(ccc);
            }

            //build the lists of set and faction combinations within the groups of similar card counts
            var groupedOf3s = GetGroupedOfs(of3);
            var groupedOf2s = GetGroupedOfs(of2);
            var groupedOf1s = GetGroupedOfs(of1);

            //to ensure that the same decklist in any order produces the same code, do some sorting
            groupedOf3s = SortGroupOf(groupedOf3s);
            groupedOf2s = SortGroupOf(groupedOf2s);
            groupedOf1s = SortGroupOf(groupedOf1s);

            //Nofs (since rare) are simply sorted by the card code - there's no optimiziation based upon the card count
            //ofN = ofN.OrderBy(c => c.CardCode).ToList();
            ofN.Sort(SortByCardCode);

            //Encode
            EncodeGroupOf(result, groupedOf3s);
            EncodeGroupOf(result, groupedOf2s);
            EncodeGroupOf(result, groupedOf1s);

            //Cards with 4+ counts are handled differently: simply [count] [card code] for each
            EncodeNOfs(result, ofN);

            return result.ToArray();
        }

        private static void EncodeNOfs(List<byte> bytes, List<CardCodeAndCount> nOfs)
        {
            foreach (var ccc in nOfs)
            {
                bytes.AddRange(Varint.GetVarint(ccc.Count));

                ParseCardCode(ccc.CardCode, out var setNumber, out var factionCode, out var cardNumber);
                var factionNumber = FactionCodeToIntIdentifier[factionCode];

                bytes.AddRange(Varint.GetVarint(setNumber));
                bytes.AddRange(Varint.GetVarint(factionNumber));
                bytes.AddRange(Varint.GetVarint(cardNumber));
            }
        }

        //The sorting convention of this encoding scheme is
        //First by the number of set/faction combinations in each top-level list
        //Second by the alphanumeric order of the card codes within those lists.
        private static List<List<CardCodeAndCount>> SortGroupOf(List<List<CardCodeAndCount>> groupOf)
        {
            groupOf.Sort(SortByCount);
            foreach (var group in groupOf)
            {
                group.Sort(SortByCardCode);
            }

            return groupOf;
        }

        private static int SortByCardCode(CardCodeAndCount x, CardCodeAndCount y)
        {
            return string.CompareOrdinal(x?.CardCode, y?.CardCode);
        }


        private static int SortByCount(List<CardCodeAndCount> x, List<CardCodeAndCount> y)
        {
            return x.Count.CompareTo(y.Count);
        }

        private static void ParseCardCode(string code, out int set, out string faction, out int number)
        {
            var codeSpan = code.AsSpan();
            set = int.Parse(codeSpan.Slice(0,2));
            faction = new string(codeSpan.Slice(2,2));
            number = int.Parse(codeSpan.Slice(4, 3));
        }

        private static List<List<CardCodeAndCount>> GetGroupedOfs(List<CardCodeAndCount> list)
        {
            var result = new List<List<CardCodeAndCount>>();
            while (list.Count > 0)
            {
                var currentSet = new List<CardCodeAndCount>();
                var current = list[0];
                //get info from first
                var firstCardCode = current.CardCode;
                ParseCardCode(firstCardCode, out var setNumber, out var factionCode, out _);

                //now add that to our new list, remove from old
                currentSet.Add(current);
                list.Remove(current);

                //sweep through rest of list and grab entries that should live with our first one.
                //matching means same set and faction - we are already assured the count matches from previous grouping.
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    current = list[i];
                    var currentCardCode = current.CardCode.AsSpan();
                    var currentSetNumber = int.Parse(currentCardCode.Slice(0, 2));
                    var currentFactionCode = currentCardCode.Slice(2, 2);

                    if (currentSetNumber == setNumber && currentFactionCode.SequenceEqual(factionCode))
                    {
                        currentSet.Add(current);
                        list.Remove(current);
                    }
                }

                result.Add(currentSet);
            }

            return result;
        }

        private static void EncodeGroupOf(List<byte> bytes, List<List<CardCodeAndCount>> groupOf)
        {
            bytes.AddRange(Varint.GetVarint(groupOf.Count));
            foreach (var currentList in groupOf)
            {
                //how many cards in current group?
                bytes.AddRange(Varint.GetVarint(currentList.Count));

                //what is this group, as identified by a set and faction pair
                var currentCardCode = currentList[0].CardCode;
                ParseCardCode(currentCardCode, out var currentSetNumber, out var currentFactionCode, out _);
                var currentFactionNumber = FactionCodeToIntIdentifier[currentFactionCode];
                bytes.AddRange(Varint.GetVarint(currentSetNumber));
                bytes.AddRange(Varint.GetVarint(currentFactionNumber));

                //what are the cards, as identified by the third section of card code only now, within this group?
                foreach (var cd in currentList)
                {
                    var code = cd.CardCode.AsSpan();
                    var sequenceNumber = int.Parse(code.Slice(4, 3));
                    bytes.AddRange(Varint.GetVarint(sequenceNumber));
                }
            }
        }

        public static bool ValidCardCodesAndCounts(List<CardCodeAndCount> deck)
        {
            foreach (var ccc in deck)
            {
                if (ccc.CardCode.Length != CARD_CODE_LENGTH)
                    return false;

                var cardCodeSpan = ccc.CardCode.AsSpan();
                if (!int.TryParse(cardCodeSpan.Slice(0, 2), out _))
                    return false;

                var faction = ccc.CardCode.Substring(2, 2);
                if (!FactionCodeToIntIdentifier.ContainsKey(faction))
                    return false;

                if (!int.TryParse(cardCodeSpan.Slice(4, 3), out _))
                    return false;

                if (ccc.Count < 1)
                    return false;
            }

            return true;
        }
    }
}