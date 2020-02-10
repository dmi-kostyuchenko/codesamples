using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Logic.Model
{
    public class ReprintingViewModel : IDataErrorInfo, INotifyPropertyChanged
    {
        public ReprintingViewModel(String count)
        {
            Count = count;
        }

        public String UniqueIdentifier
        {
            get
            {
                return _uniqueIdentifier;
            }
            set
            {
                _uniqueIdentifier = value;
                OnPropertyChanged("UniqueIdentifier");
            }
        }

        public String Index
        {
            get
            {
                return _index;
            }
            set
            {
                _index = value;
                OnPropertyChanged("Index");
            }
        }

        public String Count
        {
            get
            {
                return _count;
            }
            set
            {
                _count = value;
                OnPropertyChanged("Count");
            }
        }

        public Int32 CountValue
        {
            get
            {
                return Int32.TryParse(Count, out Int32 count) ? count : 0;
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
                    case "UniqueIdentifier":
                        if (String.IsNullOrEmpty(UniqueIdentifier)) return "Unique identifier count is required.";
                        break;
                    case "Count":
                        if (String.IsNullOrEmpty(Count)) return "Labels count is required.";
                        if (!Int32.TryParse(Count, out Int32 labelsCount)) return "Labels count should be a number.";
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

        private String _uniqueIdentifier;
        private String _count;
        private String _index;

        #endregion 
    }
}
