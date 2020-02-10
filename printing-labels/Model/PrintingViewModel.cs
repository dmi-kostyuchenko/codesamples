using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Logic.Model
{
    public class PrintingViewModel : IDataErrorInfo, INotifyPropertyChanged
    {
        public PrintingViewModel(String uniqueID, String index, String uniqueIDsCount, String count, Int32 indexMaxLength)
        {
            uniqueIdentifier = uniqueID;
            UniqueIdentifier = uniqueID;
            Index = index;
            UniqueIDsCount = uniqueIDsCount;
            Count = count;
            IndexMaxLength = indexMaxLength;
        }

        public String UniqueIdentifier
        {
            get
            {
                return uniqueIdentifier;
            }
            set
            {
                uniqueIdentifier = value;
                OnPropertyChanged("UniqueIdentifier");
            }
        }

        public String Index { get; set; }

        public Int32 IndexMaxLength { get; }

        public String Count { get; set; }

        public String UniqueIDsCount { get; set; }

        public Int32 CountValue
        {
            get
            {
                return Int32.TryParse(Count, out Int32 count) ? count : 0;
            }
        }

        public Int32 UniqueIDsCountValue
        {
            get
            {
                return Int32.TryParse(UniqueIDsCount, out Int32 count) ? count : 0;
            }
        }

        public Int32 ErrorsCount { get; set; }

        public Boolean IsValid
        {
            get
            {
                return ErrorsCount <= 0;
            }
        }

        public String LastError
        {
            get
            {
                return (this as IDataErrorInfo)["Index"] ??
                       (this as IDataErrorInfo)["Count"];
            }
        }

        #region IDataErrorInfo Members

        string IDataErrorInfo.Error
        {
            get { return null; }
        }

        string IDataErrorInfo.this[String columnName]
        {
            get
            {
                switch (columnName)
                {
                    case "Index":
                        if (String.IsNullOrEmpty(Index)) return "Index value is required.";
                        if (Index.Length > IndexMaxLength) return $"Index value cannot be longer than {IndexMaxLength} symbols.";
                        break;
                    case "Count":
                        if (String.IsNullOrEmpty(Count)) return "Labels count is required.";
                        if (!Int32.TryParse(Count, out Int32 labelsCount)) return "Labels count should be a number.";
                        break;
                    case "UniqueIDsCount":
                        if (String.IsNullOrEmpty(UniqueIDsCount)) return "Unique IDs count is required.";
                        if (!Int32.TryParse(UniqueIDsCount, out Int32 idsCount)) return "Unique IDs count should be a number.";
                        break;
                }

                return null;
            }
        }

        #endregion

        #region INotifyPropertyChanged members

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies objects registered to receive this event that a property value has changed.
        /// </summary>
        /// <param name="propertyName">The name of the property that was changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion 

        #region private fields 

        private String uniqueIdentifier;

        #endregion 
    }
}
