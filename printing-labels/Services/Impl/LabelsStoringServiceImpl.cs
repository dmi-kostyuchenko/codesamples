using Core.Data.Entities;
using Core.Logging;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services.Impl
{
    public class LabelsStoringServiceImpl : ILabelsStoringService
    {
        private readonly IIdentifiersGeneratingService _identifiersGeneratingService;
        private readonly ILogger _logger;

        public LabelsStoringServiceImpl(IIdentifiersGeneratingService identifiersGeneratingService, ILogger logger)
        {
            _identifiersGeneratingService = identifiersGeneratingService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the next printing label model
        /// </summary>
        /// <param name="connectionModel">The database connection model</param>
        /// <param name="tables">The database context tables names</param>
        /// <returns></returns>
        public String GetNextLabelIdentifier(DbContext context)
        {
            var last = context.Set<LabelEntity>().OrderByDescending(x => x.ID).FirstOrDefault();
            return _identifiersGeneratingService.NextIdentifier(last?.UniqueID ?? String.Empty);
        }

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
        public LabelEntity ReserveLabelToPrint(DbContext context, String uniqueID, String index, DateTime date, Int32 count, out Boolean isUniqueIDMatch)
        {
            var currentID = GetNextLabelIdentifier(context);
            //race case is still possible here
            var result = context.Set<LabelEntity>().Add(new LabelEntity
            {
                UniqueID = currentID,
                CreateDate = date,
                Customer = index,
                Status = 0,
                ItemCount = count,
            });
            context.SaveChanges();

            isUniqueIDMatch = currentID == uniqueID;
            return result;
        }

        /// <summary>
        /// Reverts changes related to the new label creating
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="entity">The label entity</param>
        /// <returns></returns>
        public Boolean RevertChanges(DbContext context, LabelEntity entity)
        {
            try
            {
                if (entity != null)
                {
                    context.Set<LabelEntity>().Remove(entity);
                    context.SaveChanges();

                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.FatalException(ErrorCodes.RevertingChanges, e);
                
            }

            return false;
        }

        /// <summary>
        /// Reverts changes related to the labels creating
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="entities">The label entities</param>
        /// <returns></returns>
        public Boolean RevertChanges(DbContext context, IEnumerable<LabelEntity> entities)
        {
            try
            {
                if (entities != null && entities.Any())
                {
                    context.Set<LabelEntity>().RemoveRange(entities);
                    context.SaveChanges();

                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.FatalException(ErrorCodes.RevertingChanges, e);

            }

            return false;
        }

        /// <summary>
        /// Gets the label by its unique ID
        /// </summary>
        /// <param name="context">The db context</param>
        /// <param name="uniqueId">The unique ID of label</param>
        /// <returns></returns>
        public LabelEntity GetLabelByUniqueId(DbContext context, String uniqueId)
        {
            return context.Set<LabelEntity>().FirstOrDefault(x => x.UniqueID.Trim() == uniqueId);
        }
    }
}
