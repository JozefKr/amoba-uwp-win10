using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amoba.Model
{
    /// <summary>
    /// Egyetlen játékmenet eredményét tárolja.
    /// </summary>
    public class GameResult
    {
        /// <summary>
        /// A győztes (Cross, Circle) vagy Döntetlen (None).
        /// </summary>
        public IconType Winner { get; set; } = IconType.None;

        /// <summary>
        /// A győzelmet alkotó cellák ID-jainak listája.
        /// Döntetlen vagy folyamatban lévő játéknál ez null vagy üres.
        /// </summary>
        public List<int> WinningCellIDs { get; set; } = new List<int>();
    }
}
