using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupTherapyRadio
{
    public class TherapyItem
    {

        private string _title;

        public string Id { get; set; }
        public int EpisodeNumber => int.Parse(Id.Replace("abgt", ""));
        public DateTime PublishDate { get; set; }

        public string Title
        {
            get { return FormatTitle(); }
            set { _title = value; }
        }

        public Uri DownloadUri { get; set; }

        private string FormatTitle()
        {
            var returnString = @"Above & Beyond - Group Therapy Radio";
            returnString = returnString + _title.Replace("Episode", "").Replace(@"/ ", "(") + ")";
            return returnString;
        }
    }
}
