using Core.Data.Entities;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services
{
    public interface ILabelsStoringService
    {
        /// <summary>
        /// Gets the next printing label model
        /// </summary>
        /// <param name="connectionModel">The database connection model</param>
        /// <param name="tables">The database context tables names</param>
        /// <returns></returns>
        String GetNextLabelIdentifier(DbContext context);

        /// <summary>
        /// Reserves the identifier for printing in the database by saving the new label
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="uniqueID">The label unique indentifier</param>
        /// <param name="index">The label index value</param>
        /// <param name="date">The label printing date</param>
        /// <param name="count">The count of labels</param>
        /// <param name="isUniqueIDMatch">Flag describing when the current unique ID matches the new one</param>
        /// <returns></returns>
        LabelEntity ReserveLabelToPrint(DbContext context, String uniqueID, String index, DateTime date, Int32 count, out Boolean isUniqueIDMatch);

        /// <summary>
        /// Reverts changes related to the new label creating
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="entity">The label entity</param>
        /// <returns></returns>
        Boolean RevertChanges(DbContext context, LabelEntity entity);

        /// <summary>
        /// Reverts changes related to the labels creating
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="entities">The label entities</param>
        /// <returns></returns>
        Boolean RevertChanges(DbContext context, IEnumerable<LabelEntity> entities);

        /// <summary>
        /// Gets the label by its unique ID
        /// </summary>
        /// <param name="context">The db context</param>
        /// <param name="uniqueId">The unique ID of label</param>
        /// <returns></returns>
        LabelEntity GetLabelByUniqueId(DbContext context, String uniqueId);
    }
}
