using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.Logic.Models;
using Core.Logic.Services.Properties;
using Core.Logic.Services.Utils;
using Core.Logic.Utils;
using Microsoft.Bot.Builder.CognitiveServices.QnAMaker;
using Microsoft.Bot.Builder.Luis.Models;

namespace Core.Logic.Services.Impl
{
    [Serializable]
    public class AnswersProcessingServiceImpl : IAnswersProcessingService
    {
        private readonly IDataQueryService _dataQueryService;
        private readonly IConfigurationProvider _configurationProvider; 

        public AnswersProcessingServiceImpl(IDataQueryService dataQueryService, IConfigurationProvider configurationProvider)
        {
            _dataQueryService = dataQueryService;
            _configurationProvider = configurationProvider;
        }

        /// <summary>
        /// Processes the confident QnA Maker answer and get results
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="answers">The answers</param>
        /// <returns></returns>
        public String ProcessConfidentAnswerAndGetResult(DbContext context, QnAMakerResult answer)
        {
            var parsedAnswers = ParseAnswers(answer);
            var result = SelectAnswer(parsedAnswers);
            return SubstitutePlaceholders(context, result);
        }

        /// <summary>
        /// Processes the confident QnA Maker answer and get results
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="answers">The answers</param>
        /// <param name="entities">The entities</param>
        /// <returns></returns>
        public String ProcessConfidentAnswerAndGetResult(DbContext context, String question, QnAMakerResult answer, 
                                                         IEnumerable<EntityRecommendation> entities)
        {
            var parsedAnswers = ParseAnswers(answer);
            var result = SelectAnswer(parsedAnswers);

            var parameters = LuisEntitiesHelper.ParseEntities(question, entities);
            if (parameters != null)
            {
                result = SubstituteParameters(result, parameters);
            }

            return SubstitutePlaceholders(context, result, parameters);
        }

        /// <summary>
        /// Processes unconfident QnA Maker answer and get results
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="answers">The answers</param>
        /// <returns></returns>
        public IEnumerable<QnAMakerResult> ProcessUnconfidentAnswer(DbContext context, IEnumerable<QnAMakerResult> answers)
        {
            if (!answers.Any()) return answers;

            foreach(var answer in answers)
            {
                var parsed = ParseAnswers(answer);
                var result = SelectAnswer(parsed);
                answer.Answer = SubstitutePlaceholders(context, result);
            }

            return answers;
        }

        /// <summary>
        /// Processes unconfident QnA Maker answer and get results
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="answers">The answers</param>
        /// <param name="entities">The entities</param>
        /// <returns></returns>
        public IEnumerable<QnAMakerResult> ProcessUnconfidentAnswer(DbContext context, String question, IEnumerable<QnAMakerResult> answers, 
                                                                    IEnumerable<EntityRecommendation> entities)
        {
            if (!answers.Any()) return answers;

            var parameters = LuisEntitiesHelper.ParseEntities(question, entities);
            foreach (var answer in answers)
            {
                var parsed = ParseAnswers(answer);
                var result = SelectAnswer(parsed);

                if (parameters != null)
                {
                    result = SubstituteParameters(result, parameters);
                }
                answer.Answer = SubstitutePlaceholders(context, result, parameters);
            }

            return answers;
        }

        private IEnumerable<QnAMakerAnswerModel> ParseAnswers(QnAMakerResult answer)
        {
            return answer.Answer.FromJsonSafe<String[]>()
                               ?.Select(y => new QnAMakerAnswerModel
                               {
                                   Answer = y,
                                   Score = answer.Score
                               })
                   ??
                   new QnAMakerAnswerModel[] { //TODO: parse CSV here
                       new QnAMakerAnswerModel
                       {
                           Answer = answer.Answer,
                           Score = answer.Score
                       }
                    };
        }

        private String SelectAnswer(IEnumerable<QnAMakerAnswerModel> answers)
        {
            var random = new Random().Next(0, Int32.MaxValue) % (answers.Count());
            return answers.ElementAt(random).Answer;
        }

        private String SubstitutePlaceholders(DbContext context, String answer, Dictionary<String, Object> parameters = null)
        {
            var answerBuilder = new StringBuilder(answer);
            var regex = new Regex(_configurationProvider["AnswerPlaceholderRegex"]);
            foreach (Match match in regex.Matches(answer))
            {
                var placeholderValue = match.Groups[0].Value;
                var placeholder = match.Groups.Count > 1 ? match.Groups[1].Value : null;
                if(!String.IsNullOrEmpty(placeholder))
                {
                    var data = parameters != null ? _dataQueryService.GetDataByPlaceholder(context, placeholder, parameters)
                                                  : _dataQueryService.GetDataByPlaceholder(context, placeholder);
                    if (!String.IsNullOrEmpty(data))
                    {
                        data = data.Contains(Environment.NewLine) ? data.Insert(0, Environment.NewLine) : data;
                        answerBuilder.Replace(placeholderValue, data);
                    }
                    else
                    {
                        return ResourcesDE.NoResultsAvailable;
                    }
                }
            }

            answerBuilder.Replace(Environment.NewLine, "<br/>"); //TODO: check this replacement
            return answerBuilder.ToString();
        }

        private String SubstituteParameters(String answer, Dictionary<String, Object> parameters)
        {
            var answerBuilder = new StringBuilder(answer);
            var regex = new Regex(_configurationProvider["AnswerParameterRegex"]);
            foreach (Match match in regex.Matches(answer))
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
                    else
                    {
                        return ResourcesDE.QuestionParametersNotRecognized;
                    }
                }
            }

            return answerBuilder.ToString();
        }
    }
}
