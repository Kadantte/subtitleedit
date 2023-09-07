﻿using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class Statistics : PositionAndSizeForm
    {

        public class StingOrdinalComparer : IEqualityComparer<string>, IComparer<string>
        {
            public bool Equals(string x, string y)
            {
                if (x == null)
                {
                    return y == null;
                }

                return x.Equals(y, StringComparison.Ordinal);
            }

            public int GetHashCode(string x)
            {
                return x.GetHashCode();
            }

            public int Compare(string x, string y)
            {
                return string.CompareOrdinal(x, y);
            }
        }

        private readonly Subtitle _subtitle;
        private readonly SubtitleFormat _format;
        private readonly LanguageStructure.Statistics _l;
        private string _mostUsedLines;
        private string _general;
        private int _totalWords;
        private string _mostUsedWords;
        private const string WriteFormat = @"File generated by: Subtitle Edit
https://www.nikse.dk/subtitleedit/
https://github.com/SubtitleEdit/subtitleedit
============================= General =============================
{0}
============================= Most Used Words =============================
{1}
============================= Most Used Lines =============================
{2}";
        private readonly string _fileName;

        private static readonly char[] ExpectedChars = { '♪', '♫', '"', '(', ')', '[', ']', ' ', ',', '!', '?', '.', ':', ';', '-', '_', '@', '<', '>', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '،', '؟', '؛' };

        public Statistics(Subtitle subtitle, string fileName, SubtitleFormat format)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            _subtitle = subtitle;
            _format = format;
            _fileName = fileName;

            _l = LanguageSettings.Current.Statistics;
            Text = string.IsNullOrEmpty(fileName) ? _l.Title : string.Format(_l.TitleWithFileName, fileName);
            groupBoxGeneral.Text = _l.GeneralStatistics;
            groupBoxMostUsed.Text = _l.MostUsed;
            labelMostUsedWords.Text = _l.MostUsedWords;
            labelMostUsedLines.Text = _l.MostUsedLines;
            buttonExport.Text = _l.Export;
            buttonOK.Text = LanguageSettings.Current.General.Ok;
            UiUtil.FixLargeFonts(this, buttonOK);

            CalculateWordStatistics();
            CalculateGeneralStatistics();
            textBoxGeneral.Text = _general;
            textBoxGeneral.SelectionStart = 0;
            textBoxGeneral.SelectionLength = 0;
            textBoxGeneral.ScrollToCaret();
            textBoxMostUsedWords.Text = _mostUsedWords;

            CalculateMostUsedLines();
            textBoxMostUsedLines.Text = _mostUsedLines;
        }

        private void CalculateGeneralStatistics()
        {
            if (_subtitle == null || _subtitle.Paragraphs.Count == 0)
            {
                textBoxGeneral.Text = _l.NothingFound;
                return;
            }

            var allText = new StringBuilder();
            int minimumLineLength = 99999999;
            int maximumLineLength = 0;
            long totalLineLength = 0;
            int minimumSingleLineLength = 99999999;
            int maximumSingleLineLength = 0;
            long totalSingleLineLength = 0;
            long totalSingleLines = 0;
            int minimumSingleLineWidth = 99999999;
            int maximumSingleLineWidth = 0;
            long totalSingleLineWidth = 0;
            double minimumDuration = 100000000;
            double maximumDuration = 0;
            double totalDuration = 0;
            double minimumCharsSec = 100000000;
            double maximumCharsSec = 0;
            double totalCharsSec = 0;
            double minimumWpm = 100000000;
            double maximumWpm = 0;
            double totalWpm = 0;
            var gapMinimum = double.MaxValue;
            var gapMaximum = 0d;
            var gapTotal = 0d;

            var aboveOptimalCpsCount = 0;
            var aboveMaximumCpsCount = 0;
            var aboveMaximumWpmCount = 0;
            var belowMinimumDurationCount = 0;
            var aboveMaximumDurationCount = 0;
            var aboveMaximumLineLengthCount = 0;
            var aboveMaximumLineWidthCount = 0;
            var belowMinimumGapCount = 0;

            foreach (var p in _subtitle.Paragraphs)
            {
                allText.Append(p.Text);

                var len = GetLineLength(p);
                minimumLineLength = Math.Min(minimumLineLength, len);
                maximumLineLength = Math.Max(len, maximumLineLength);
                totalLineLength += len;

                var duration = p.DurationTotalMilliseconds;
                minimumDuration = Math.Min(duration, minimumDuration);
                maximumDuration = Math.Max(duration, maximumDuration);
                totalDuration += duration;

                var charsSec = Utilities.GetCharactersPerSecond(p);
                minimumCharsSec = Math.Min(charsSec, minimumCharsSec);
                maximumCharsSec = Math.Max(charsSec, maximumCharsSec);
                totalCharsSec += charsSec;

                var wpm = p.WordsPerMinute;
                minimumWpm = Math.Min(wpm, minimumWpm);
                maximumWpm = Math.Max(wpm, maximumWpm);
                totalWpm += wpm;

                var next = _subtitle.GetParagraphOrDefault(_subtitle.GetIndex(p) + 1);
                if (next != null)
                {
                    var gap = next.StartTime.TotalMilliseconds - p.EndTime.TotalMilliseconds;
                    if (gap < gapMinimum)
                    {
                        gapMinimum = gap;
                    }

                    if (gap > gapMaximum)
                    {
                        gapMaximum = gap;
                    }

                    if (gap < Configuration.Settings.General.MinimumMillisecondsBetweenLines)
                    {
                        belowMinimumGapCount++;
                    }

                    gapTotal += gap;
                }

                foreach (var line in p.Text.SplitToLines())
                {
                    var l = GetSingleLineLength(line);
                    minimumSingleLineLength = Math.Min(l, minimumSingleLineLength);
                    maximumSingleLineLength = Math.Max(l, maximumSingleLineLength);
                    totalSingleLineLength += l;

                    if (l > Configuration.Settings.General.SubtitleLineMaximumLength)
                    {
                        aboveMaximumLineLengthCount++;
                    }

                    if (Configuration.Settings.Tools.ListViewSyntaxColorWideLines)
                    {
                        var w = GetSingleLineWidth(line);
                        minimumSingleLineWidth = Math.Min(w, minimumSingleLineWidth);
                        maximumSingleLineWidth = Math.Max(w, maximumSingleLineWidth);
                        totalSingleLineWidth += w;

                        if (w > Configuration.Settings.General.SubtitleLineMaximumPixelWidth)
                        {
                            aboveMaximumLineWidthCount++;
                        }
                    }

                    totalSingleLines++;
                }

                var cps = Utilities.GetCharactersPerSecond(p);
                if (cps > Configuration.Settings.General.SubtitleOptimalCharactersPerSeconds)
                {
                    aboveOptimalCpsCount++;
                }
                if (cps > Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds)
                {
                    aboveMaximumCpsCount++;
                }

                if (p.WordsPerMinute > Configuration.Settings.General.SubtitleMaximumWordsPerMinute)
                {
                    aboveMaximumWpmCount++;
                }

                if (p.DurationTotalMilliseconds < Configuration.Settings.General.SubtitleMinimumDisplayMilliseconds)
                {
                    belowMinimumDurationCount++;
                }
                if (p.DurationTotalMilliseconds > Configuration.Settings.General.SubtitleMaximumDisplayMilliseconds)
                {
                    aboveMaximumDurationCount++;
                }
            }

            var sb = new StringBuilder();
            var sourceLength = _subtitle.ToText(_format).Length;
            var allTextToLower = allText.ToString().ToLowerInvariant();

            sb.AppendLine(string.Format(_l.NumberOfLinesX, _subtitle.Paragraphs.Count));
            sb.AppendLine(string.Format(_l.LengthInFormatXinCharactersY, _format.FriendlyName, sourceLength));
            sb.AppendLine(string.Format(_l.NumberOfCharactersInTextOnly, allText.ToString().CountCharacters(false)));
            sb.AppendLine(string.Format(_l.TotalDuration, new TimeCode(totalDuration).ToDisplayString()));
            sb.AppendLine(string.Format(_l.TotalCharsPerSecond, (double)allText.ToString().CountCharacters(true) / (totalDuration / TimeCode.BaseUnit)));
            sb.AppendLine(string.Format(_l.TotalWords, _totalWords));
            sb.AppendLine(string.Format(_l.NumberOfItalicTags, Utilities.CountTagInText(allTextToLower, "<i>")));
            sb.AppendLine(string.Format(_l.NumberOfBoldTags, Utilities.CountTagInText(allTextToLower, "<b>")));
            sb.AppendLine(string.Format(_l.NumberOfUnderlineTags, Utilities.CountTagInText(allTextToLower, "<u>")));
            sb.AppendLine(string.Format(_l.NumberOfFontTags, Utilities.CountTagInText(allTextToLower, "<font ")));
            sb.AppendLine(string.Format(_l.NumberOfAlignmentTags, Utilities.CountTagInText(allTextToLower, "{\\an")));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.LineLengthMinimum, minimumLineLength) + " (" + GetIndicesWithLength(minimumLineLength) + ")");
            sb.AppendLine(string.Format(_l.LineLengthMaximum, maximumLineLength) + " (" + GetIndicesWithLength(maximumLineLength) + ")");
            sb.AppendLine(string.Format(_l.LineLengthAverage, totalLineLength / _subtitle.Paragraphs.Count));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.LinesPerSubtitleAverage, (double)totalSingleLines / _subtitle.Paragraphs.Count));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.SingleLineLengthMinimum, minimumSingleLineLength) + " (" + GetIndicesWithSingleLineLength(minimumSingleLineLength) + ")");
            sb.AppendLine(string.Format(_l.SingleLineLengthMaximum, maximumSingleLineLength) + " (" + GetIndicesWithSingleLineLength(maximumSingleLineLength) + ")");
            sb.AppendLine(string.Format(_l.SingleLineLengthAverage, totalSingleLineLength / totalSingleLines));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.SingleLineLengthExceedingMaximum, Configuration.Settings.General.SubtitleLineMaximumLength, aboveMaximumLineLengthCount, ((double)aboveMaximumLineLengthCount / _subtitle.Paragraphs.Count) * 100.0));
            sb.AppendLine();

            if (Configuration.Settings.Tools.ListViewSyntaxColorWideLines)
            {
                sb.AppendLine(string.Format(_l.SingleLineWidthMinimum, minimumSingleLineWidth) + " (" + GetIndicesWithSingleLineWidth(minimumSingleLineWidth) + ")");
                sb.AppendLine(string.Format(_l.SingleLineWidthMaximum, maximumSingleLineWidth) + " (" + GetIndicesWithSingleLineWidth(maximumSingleLineWidth) + ")");
                sb.AppendLine(string.Format(_l.SingleLineWidthAverage, totalSingleLineWidth / totalSingleLines));
                sb.AppendLine();
                sb.AppendLine(string.Format(_l.SingleLineWidthExceedingMaximum, Configuration.Settings.General.SubtitleLineMaximumPixelWidth, aboveMaximumLineWidthCount, ((double)aboveMaximumLineWidthCount / _subtitle.Paragraphs.Count) * 100.0));
                sb.AppendLine();
            }

            sb.AppendLine(string.Format(_l.DurationMinimum, minimumDuration / TimeCode.BaseUnit) + " (" + GetIndicesWithDuration(minimumDuration) + ")");
            sb.AppendLine(string.Format(_l.DurationMaximum, maximumDuration / TimeCode.BaseUnit) + " (" + GetIndicesWithDuration(maximumDuration) + ")");
            sb.AppendLine(string.Format(_l.DurationAverage, totalDuration / _subtitle.Paragraphs.Count / TimeCode.BaseUnit));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.DurationExceedingMinimum, Configuration.Settings.General.SubtitleMinimumDisplayMilliseconds / TimeCode.BaseUnit, belowMinimumDurationCount, ((double)belowMinimumDurationCount / _subtitle.Paragraphs.Count) * 100.0));
            sb.AppendLine(string.Format(_l.DurationExceedingMaximum, Configuration.Settings.General.SubtitleMaximumDisplayMilliseconds / TimeCode.BaseUnit, aboveMaximumDurationCount, ((double)aboveMaximumDurationCount / _subtitle.Paragraphs.Count) * 100.0));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.CharactersPerSecondMinimum, minimumCharsSec) + " (" + GetIndicesWithCps(minimumCharsSec) + ")");
            sb.AppendLine(string.Format(_l.CharactersPerSecondMaximum, maximumCharsSec) + " (" + GetIndicesWithCps(maximumCharsSec) + ")");
            sb.AppendLine(string.Format(_l.CharactersPerSecondAverage, totalCharsSec / _subtitle.Paragraphs.Count));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.CharactersPerSecondExceedingOptimal, Configuration.Settings.General.SubtitleOptimalCharactersPerSeconds, aboveOptimalCpsCount, ((double)aboveOptimalCpsCount / _subtitle.Paragraphs.Count) * 100.0));
            sb.AppendLine(string.Format(_l.CharactersPerSecondExceedingMaximum, Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds, aboveMaximumCpsCount, ((double)aboveMaximumCpsCount / _subtitle.Paragraphs.Count) * 100.0));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.WordsPerMinuteMinimum, minimumWpm) + " (" + GetIndicesWithWpm(minimumWpm) + ")");
            sb.AppendLine(string.Format(_l.WordsPerMinuteMaximum, maximumWpm) + " (" + GetIndicesWithWpm(maximumWpm) + ")");
            sb.AppendLine(string.Format(_l.WordsPerMinuteAverage, totalWpm / _subtitle.Paragraphs.Count));
            sb.AppendLine();
            sb.AppendLine(string.Format(_l.WordsPerMinuteExceedingMaximum, Configuration.Settings.General.SubtitleMaximumWordsPerMinute, aboveMaximumWpmCount, ((double)aboveMaximumWpmCount / _subtitle.Paragraphs.Count) * 100.0));
            sb.AppendLine();

            if (_subtitle.Paragraphs.Count > 1)
            {
                sb.AppendLine(string.Format(_l.GapMinimum, gapMinimum) + " (" + GetIndicesWithGap(gapMinimum) + ")");
                sb.AppendLine(string.Format(_l.GapMaximum, gapMaximum) + " (" + GetIndicesWithGap(gapMaximum) + ")");
                sb.AppendLine(string.Format(_l.GapAverage, gapTotal / _subtitle.Paragraphs.Count - 1));
                sb.AppendLine();
                sb.AppendLine(string.Format(_l.GapExceedingMinimum, Configuration.Settings.General.MinimumMillisecondsBetweenLines, belowMinimumGapCount, ((double)belowMinimumGapCount / _subtitle.Paragraphs.Count) * 100.0));
                sb.AppendLine();
            }

            _general = sb.ToString().Trim();
        }

        private static int GetLineLength(Paragraph p)
        {
            return p.Text.Replace(Environment.NewLine, string.Empty).CountCharacters(Configuration.Settings.General.CpsLineLengthStrategy, false);
        }

        private static int GetSingleLineLength(string s)
        {
            return s.CountCharacters(Configuration.Settings.General.CpsLineLengthStrategy, false);
        }

        private static int GetSingleLineWidth(string s)
        {
            return TextWidth.CalcPixelWidth(HtmlUtil.RemoveHtmlTags(s, true));
        }

        private const int NumberOfLinesToShow = 10;

        private string GetIndicesWithDuration(double duration)
        {
            var indices = new List<string>();
            for (var i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var p = _subtitle.Paragraphs[i];
                if (Math.Abs(p.DurationTotalMilliseconds - duration) < 0.01)
                {
                    if (indices.Count >= NumberOfLinesToShow)
                    {
                        indices.Add("...");
                        break;
                    }
                    indices.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }
            }
            return string.Join(", ", indices);
        }

        private string GetIndicesWithCps(double cps)
        {
            var indices = new List<string>();
            for (var i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var p = _subtitle.Paragraphs[i];
                if (Math.Abs(Utilities.GetCharactersPerSecond(p) - cps) < 0.01)
                {
                    if (indices.Count >= NumberOfLinesToShow)
                    {
                        indices.Add("...");
                        break;
                    }
                    indices.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }
            }

            return string.Join(", ", indices);
        }

        private string GetIndicesWithWpm(double wpm)
        {
            var indices = new List<string>();
            for (var i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var p = _subtitle.Paragraphs[i];
                if (Math.Abs(p.WordsPerMinute - wpm) < 0.01)
                {
                    if (indices.Count >= NumberOfLinesToShow)
                    {
                        indices.Add("...");
                        break;
                    }
                    indices.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }
            }

            return string.Join(", ", indices);
        }

        private string GetIndicesWithGap(double cps)
        {
            var indices = new List<string>();
            for (var i = 0; i < _subtitle.Paragraphs.Count-1; i++)
            {
                var p = _subtitle.Paragraphs[i];
                var next = _subtitle.Paragraphs[i+1];
                var gap = next.StartTime.TotalMilliseconds - p.EndTime.TotalMilliseconds;
                if (Math.Abs(gap - cps) < 0.01)
                {
                    if (indices.Count >= NumberOfLinesToShow)
                    {
                        indices.Add("...");
                        break;
                    }
                    indices.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }
            }

            return string.Join(", ", indices);
        }

        private string GetIndicesWithLength(int length)
        {
            var indices = new List<string>();
            for (var i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var p = _subtitle.Paragraphs[i];
                if (GetLineLength(p) == length)
                {
                    if (indices.Count >= NumberOfLinesToShow)
                    {
                        indices.Add("...");
                        break;
                    }
                    indices.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                }
            }
            return string.Join(", ", indices);
        }

        private string GetIndicesWithSingleLineLength(int length)
        {
            var indices = new List<string>();
            for (var i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var p = _subtitle.Paragraphs[i];
                foreach (var line in p.Text.SplitToLines())
                {
                    if (GetSingleLineLength(line) == length)
                    {
                        if (indices.Count >= NumberOfLinesToShow)
                        {
                            indices.Add("...");
                            return string.Join(", ", indices);
                        }
                        indices.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                        break;
                    }
                }
            }
            return string.Join(", ", indices);
        }

        private string GetIndicesWithSingleLineWidth(int width)
        {
            var indices = new List<string>();
            for (int i = 0; i < _subtitle.Paragraphs.Count; i++)
            {
                var p = _subtitle.Paragraphs[i];
                foreach (var line in p.Text.SplitToLines())
                {
                    if (GetSingleLineWidth(line) == width)
                    {
                        if (indices.Count >= NumberOfLinesToShow)
                        {
                            indices.Add("...");
                            return string.Join(", ", indices);
                        }
                        indices.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                        break;
                    }
                }
            }
            return string.Join(", ", indices);
        }

        private void Statistics_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyData == (Keys.Control | Keys.C))
            {
                Clipboard.SetText(string.Format(WriteFormat, _general, _mostUsedWords, _mostUsedLines), TextDataFormat.UnicodeText);
            }
        }

        private void MostUsedWordsAdd(Dictionary<string, int> hashtable, string input)
        {
            var text = input;
            if (text.Contains("< "))
            {
                text = HtmlUtil.FixInvalidItalicTags(text);
            }

            text = StripHtmlTags(text);

            var idx = text.IndexOf("<font", StringComparison.OrdinalIgnoreCase);
            var error = false;
            while (idx >= 0)
            {
                var endIdx = text.IndexOf('>', idx + 5);
                if (endIdx < idx)
                {
                    error = true;
                    break;
                }
                endIdx++;
                text = text.Remove(idx, endIdx - idx);
                idx = text.IndexOf("<font", idx, StringComparison.OrdinalIgnoreCase);
            }
            if (!error)
            {
                text = text.Replace("</font>", ".");
            }

            foreach (var word in Utilities.RemoveSsaTags(text).Split(ExpectedChars, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = word.Trim();
                if (s.Length > 1 && hashtable.ContainsKey(s))
                {
                    hashtable[s]++;
                }
                else if (s.Length > 1)
                {
                    hashtable.Add(s, 1);
                }
            }
        }

        private static void MostUsedLinesAdd(Dictionary<string, int> hashtable, string input)
        {
            var text = StripHtmlTags(input)
                .Replace('!', '.')
                .Replace('?', '.')
                .Replace("...", ".")
                .Replace("..", ".")
                .Replace('-', ' ')
                .FixExtraSpaces();

            text = Utilities.RemoveSsaTags(text);

            foreach (string line in text.Split('.'))
            {
                var s = line.Trim();
                if (hashtable.ContainsKey(s))
                {
                    hashtable[s]++;
                }
                else if (s.Length > 0 && s.Contains(' '))
                {
                    hashtable.Add(s, 1);
                }
            }
        }

        private static string StripHtmlTags(string input)
        {
            var text = input.Trim('\'').Replace("\"", string.Empty);

            if (text.Length < 8)
            {
                return text;
            }

            text = text.Replace("<i>", string.Empty);
            text = text.Replace("</i>", ".");
            text = text.Replace("<I>", string.Empty);
            text = text.Replace("</I>", ".");
            text = text.Replace("<b>", string.Empty);
            text = text.Replace("</b>", ".");
            text = text.Replace("<B>", string.Empty);
            text = text.Replace("</B>", ".");
            text = text.Replace("<u>", string.Empty);
            text = text.Replace("</u>", ".");
            text = text.Replace("<U>", string.Empty);
            text = text.Replace("</U>", ".");
            return text;
        }

        private void CalculateWordStatistics()
        {
            var hashtable = new Dictionary<string, int>(new StingOrdinalComparer());

            foreach (Paragraph p in _subtitle.Paragraphs)
            {
                MostUsedWordsAdd(hashtable, p.Text);
                _totalWords += p.Text.CountWords();
            }

            var sortedTable = new SortedDictionary<string, string>(new StingOrdinalComparer());
            foreach (KeyValuePair<string, int> item in hashtable)
            {
                if (item.Value > 1)
                {
                    sortedTable.Add($"{item.Value:0000}" + "_" + item.Key, item.Value + ": " + item.Key);
                }
            }

            var sb = new StringBuilder();
            if (sortedTable.Count > 0)
            {
                var temp = string.Empty;
                foreach (KeyValuePair<string, string> item in sortedTable)
                {
                    temp = item.Value + Environment.NewLine + temp;
                }
                sb.AppendLine(temp);
            }
            else
            {
                sb.AppendLine(_l.NothingFound);
            }
            _mostUsedWords = sb.ToString();
        }

        private void CalculateMostUsedLines()
        {
            var hashtable = new Dictionary<string, int>();

            foreach (Paragraph p in _subtitle.Paragraphs)
            {
                MostUsedLinesAdd(hashtable, p.Text.Replace(Environment.NewLine, " ").Replace("  ", " "));
            }

            var sortedTable = new SortedDictionary<string, string>(new StingOrdinalComparer());
            foreach (KeyValuePair<string, int> item in hashtable)
            {
                if (item.Value > 1)
                {
                    sortedTable.Add($"{item.Value:0000}" + "_" + item.Key, item.Value + ": " + item.Key);
                }
            }

            var sb = new StringBuilder();
            if (sortedTable.Count > 0)
            {
                var temp = string.Empty;
                foreach (KeyValuePair<string, string> item in sortedTable)
                {
                    temp = item.Value + Environment.NewLine + temp;
                }
                sb.AppendLine(temp);
            }
            else
            {
                sb.AppendLine(_l.NothingFound);
            }
            _mostUsedLines = sb.ToString();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            string preViewFileName = Path.GetFileNameWithoutExtension(_fileName) + ".Stats";
            using (var saveDialog = new SaveFileDialog { FileName = preViewFileName, Filter = LanguageSettings.Current.Main.TextFiles + " (*.txt)|*.txt|NFO files (*.nfo)|*.nfo" })
            {
                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = saveDialog.FileName;
                    var statistic = string.Format(WriteFormat, _general, _mostUsedWords, _mostUsedLines);
                    File.WriteAllText(fileName, statistic);
                }
            }
        }

    }
}
