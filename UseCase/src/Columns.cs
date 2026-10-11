using System;
using System.Collections.Generic;

namespace UseCase
{
    /// <summary>
    /// Splits a long panel into columns side by side (the card's decision 6): as few columns as the height allows, the rows
    /// shared out evenly between them, and a heading never left alone at the foot of a column. Plain logic, tested by the
    /// harness; the panel measures how many rows fit.
    /// </summary>
    internal static class Columns
    {
        /// <param name="rows">The panel's rows, in order.</param>
        /// <param name="mostRows">The most rows one column may hold (at least 2).</param>
        internal static List<List<Row>> Split(IList<Row> rows, int mostRows)
        {
            var columns = new List<List<Row>>();
            if (rows == null || rows.Count == 0)
            {
                return columns;
            }
            mostRows = Math.Max(2, mostRows);
            // The fewest columns that hold every row, each as even as the headings allow: a heading moved down to its first
            // line takes a row of room, so a column may need to be a little taller than an even share.
            for (int count = (rows.Count + mostRows - 1) / mostRows; count <= rows.Count; count++)
            {
                for (int each = (rows.Count + count - 1) / count; each <= mostRows; each++)
                {
                    List<List<Row>> tried = Fill(rows, each);
                    if (tried.Count <= count)
                    {
                        return tried;
                    }
                }
            }
            return Fill(rows, mostRows);
        }

        /// <summary>
        /// At most <paramref name="count"/> columns of up to <paramref name="mostRows"/> rows each, filled from the top (the
        /// panel's room across the screen holds no more), the last ending "and N more" for the lines left out, N counting
        /// the lines behind each row (Row.Stands). With room for every row, it is Split's even share.
        /// </summary>
        internal static List<List<Row>> Take(IList<Row> rows, int count, int mostRows)
        {
            mostRows = Math.Max(2, mostRows);
            List<List<Row>> even = Split(rows, mostRows);
            if (count <= 0 || even.Count <= count)
            {
                return even;
            }
            List<List<Row>> full = Fill(rows, mostRows);
            if (full.Count <= count)
            {
                return full;
            }
            int left = 0;
            for (int i = count; i < full.Count; i++)
            {
                foreach (Row row in full[i])
                {
                    left += row.Stands;
                }
            }
            full.RemoveRange(count, full.Count - count);
            List<Row> last = full[count - 1];
            // One row's room for "and N more": the last line goes (and a heading it would leave alone at the foot).
            while (last.Count > 1 && (last.Count >= mostRows || (last[last.Count - 1].Heading && last[last.Count - 1].Stands == 0)))
            {
                left += last[last.Count - 1].Stands;
                last.RemoveAt(last.Count - 1);
            }
            last.Add(new Row($"and {left} more", false, left));
            return full;
        }

        private static List<List<Row>> Fill(IList<Row> rows, int each)
        {
            var columns = new List<List<Row>>();
            var column = new List<Row>();
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool full = column.Count >= each;
                // A heading that would end its column goes to the next one, with its first line.
                bool lonelyHeading = row.Heading && column.Count == each - 1 && i + 1 < rows.Count && column.Count > 0;
                if ((full || lonelyHeading) && column.Count > 0)
                {
                    columns.Add(column);
                    column = new List<Row>();
                }
                column.Add(row);   // never past each, which Split keeps within mostRows
            }
            if (column.Count > 0)
            {
                columns.Add(column);
            }
            return columns;
        }
    }
}
