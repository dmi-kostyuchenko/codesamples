using Core.Data.Base;
using Core.Data.Model.Entities.Administration;
using Core.Logging;
using Core.Logic.Services.Properties;
using Core.Logic.Services.QnAMaker;
using Core.Logic.Services.Utils;
using Core.Logic.Utils;
using Microsoft.Bot.Builder.Luis.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Logic.Services.Impl
{
    [Serializable]
    public class QuestionsProcessingServiceImpl : IQuestionsProcessingService
    {
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IContextFactory _contextFactory;
        private readonly ILogger _logger;

        public QuestionsProcessingServiceImpl(IConfigurationProvider configurationProvider, IContextFactory contextFactory,
                                              ILogger logger)
        {
            _configurationProvider = configurationProvider;
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Removes the recognized entities from the question and replaces them with their types
        /// </summary>
        /// <param name="question">The question</param>
        /// <param name="entities">The LUIS entities</param>
        /// <returns></returns>
        public String PreProcessQuestionForKnowledgeBase(String question, IEnumerable<EntityRecommendation> entities)
        {
            if (entities == null && !entities.Any()) return question;

            foreach (var entity in entities)
            {
                question = ReplaceEntityInQuestion(question, entity);
            }

            return question;
        }

        /// <summary>
        /// Replaces the question parameters with their values
        /// </summary>
        /// <param name="question">The question</param>
        /// <param name="entities">The LUIS entities</param>
        /// <returns></returns>
        public IEnumerable<CustomQnAMakerResult> SubstituteQuestionParameters(String sourceQuestion, IEnumerable<CustomQnAMakerResult> answers, 
                                                                              IEnumerable<EntityRecommendation> entities)
        {
            if (entities == null || !entities.Any()) return answers;

            var parameters = LuisEntitiesHelper.ParseEntities(sourceQuestion, entities);
            foreach (var answer in answers)
            {
                answer.Questions[0] = SubstituteParameters(answer.Questions[0], parameters);
            }

            return answers;
        }


        /// <summary>
        /// Saves the unanswered question to the database for future review
        /// </summary>
        /// <param name="question">The question</param>
        /// <param name="user">The user identifier</param>
        public void ProcessNotAnsweredQuestion(String question, String user)
        {
            //wrapping into the try-catch to not interrupt the main process
            try
            {
                using (var context = _contextFactory.GetAdministrationContext())
                {
                    if (!IsQuestionDuplicate(context, question))
                    {
                        context.Set<ChatOpen>().Add(
                            new ChatOpen
                            {
                                Question = question,
                                QuestionHash = question.ToSHA256Hash(),
                                Timestamp = DateTime.UtcNow,
                                User = user
                            });

                        context.SaveChanges();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.WarnException($"Failed to save unanswered question: {question} from user {user}", e);
            }
        }

        private String ReplaceEntityInQuestion(String question, EntityRecommendation entity)
        {
            try
            {
                question = question.Remove(entity.StartIndex.Value, entity.EndIndex.Value - entity.StartIndex.Value + 1);
                return question.Insert(entity.StartIndex.Value, String.Format(_configurationProvider["AnswerParameterFormat"], entity.Type));
            }
            catch
            {
                return entity.Entity; //TODO: add failsafe spaces removing here
            }
        }

        private String SubstituteParameters(String question, Dictionary<String, Object> parameters)
        {
            var answerBuilder = new StringBuilder(question);
            var regex = new Regex(_configurationProvider["AnswerParameterRegex"]);
            foreach (Match match in regex.Matches(question))
            {
                var parameterMatch = match.Groups[0].Value;
                var parameterName = match.Groups.Count > 1 ? match.Groups[1].Value : null;
                if (!String.IsNullOrEmpty(parameterName))
                {
                    if (parameters.ContainsKey(parameterName))
                    {
                        var parameterValue = parameters[parameterName].ToString(); //TODO: check this conversion
                        answerBuilder.Replace(parameterMatch, parameterValue);
                    }
                }
            }

            return answerBuilder.ToString();
        }

        /// <summary>
        /// Check the question has duplicate in the database
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="question">The question</param>
        /// <returns></returns>
        private Boolean IsQuestionDuplicate(DbContext context, String question)
        {
            var hash = question.ToSHA256Hash();
            return context.Set<ChatOpen>().Any(x => x.QuestionHash == hash);
        }
    }
}
