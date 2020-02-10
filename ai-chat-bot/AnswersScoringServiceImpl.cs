using Core.Logic.Services.QnAMaker;
using Core.Logic.Services.Utils;
using Microsoft.Bot.Builder.Luis.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Logic.Services.Impl
{
    [Serializable]
    public class AnswersScoringServiceImpl : IAnswersScoringService
    {
        private readonly IConfigurationProvider _configurationProvider;

        public AnswersScoringServiceImpl(IConfigurationProvider configurationProvider)
        {
            _configurationProvider = configurationProvider;
        }

        public Double HighConfidenceScoreThreshold
        {
            get
            {
                return _configurationProvider.GetSetting<Double>("HighConfidenceScoreThreshold");
            }
        }

        public Double HighConfidenceDeltaThreshold
        {
            get
            {
                return _configurationProvider.GetSetting<Double>("HighConfidenceDeltaThreshold");
            }
        }

        public Double AppropriationScoreThreshold
        {
            get
            {
                return _configurationProvider.GetSetting<Double>("AppropriationScoreThreshold");
            }
        }

        /// <summary>
        /// Check the answer is confident
        /// </summary>
        /// <param name="answers">The KB answers</param>
        /// <returns></returns>
        public Boolean IsConfidentAnswer(IList<CustomQnAMakerResult> answers)
        {
            if (answers.Count <= 1 || answers.First().Score >= HighConfidenceScoreThreshold)
            {
                return true;
            }

            if (answers[0].Score - answers[1].Score > HighConfidenceDeltaThreshold)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check the answers are appropriate
        /// </summary>
        /// <param name="answers">The KB answers</param>
        /// <returns></returns>
        public Boolean IsAppropriateAnswer(IList<CustomQnAMakerResult> answers)
        {
            return answers.Count >= 1 && answers.Max(x => x.Score) > AppropriationScoreThreshold;
        }

        /// <summary>
        /// Removes answers not matching to the found parameters
        /// </summary>
        /// <param name="answers">The KB answers</param>
        /// <param name="entities">The LUIS entities</param>
        /// <returns></returns>
        public IList<CustomQnAMakerResult> RemoveAnswersNotMatchingParameters(IList<CustomQnAMakerResult> answers, IEnumerable<EntityRecommendation> entities)
        {
            if (entities != null && entities.Any())
            {
                var result = new List<CustomQnAMakerResult>(answers.Count);
                var entityTypes = entities.Select(x => x.Type.ToLower()).ToArray();

                foreach (var answer in answers)
                {
                    var parameters = GetAnswerParameters(answer.Answer);
                    if (!parameters.Any(x => !entityTypes.Contains(x.ToLower())))
                    {
                        result.Add(answer);
                    }
                }

                return result;
            }
            else
            {
                return RemoveAnswersNotMatchingParameters(answers).ToList();
            }

            
        }

        /// <summary>
        /// Removes answers not matching to the found parameters
        /// </summary>
        /// <param name="answers">The KB answers</param>
        /// <returns></returns>
        public IList<CustomQnAMakerResult> RemoveAnswersNotMatchingParameters(IList<CustomQnAMakerResult> answers)
        {
            var result = new List<CustomQnAMakerResult>(answers.Count);
            foreach (var answer in answers)
            {
                var parameters = GetAnswerParameters(answer.Answer);
                if (!answer.IsAnswerParametrized() && !parameters.Any())
                {
                    result.Add(answer);
                }
            }

            return result;
        }

        /// <summary>
        /// Recalculates scores of answers for best matching the returned entities
        /// </summary>
        /// <param name="answers">The QnA Maker answers</param>
        /// <param name="entities">The entities returned by LUIS</param>
        /// <returns></returns>
        public IEnumerable<CustomQnAMakerResult> RecalculateScores(IEnumerable<CustomQnAMakerResult> answers, IEnumerable<EntityRecommendation> entities)
        {
            //if entities > 0 and there is at least one parametrized answer, then all non-parametrized answers get down-scored by 0.2
            if (entities != null && entities.Any())
            {
                if (answers.Any(x => x.IsAnswerParametrized()))
                {
                    foreach (var answer in answers.Where(x => !x.IsAnswerParametrized()))
                    {
                        answer.Score -= QnAMakerDialogHelper.HighConfidenceDeltaThreshold;
                    }
                }
            }
            else //if entities == 0, then all parametrized answers get down-scored by 0.2
            {
                //recalculate without entities
                RecalculateScores(answers);
            }

            //if the answer contains entity type, then it gets up-scored by 0.2
            var entityTypes = entities.Select(x => x.Type.ToLower()).ToArray();
            foreach (var answer in answers.Where(x => x.IsAnswerParametrized()))
            {
                var parameters = GetAnswerParameters(answer.Answer);
                if (parameters.Any(x => entityTypes.Contains(x.ToLower())))
                {
                    answer.Score += QnAMakerDialogHelper.HighConfidenceDeltaThreshold;
                }
            }

            return answers;
        }

        /// <summary>
        /// Recalculates scores of answers for best matching the returned entities
        /// </summary>
        /// <param name="answers">The QnA Maker answers</param>
        /// <returns></returns>
        public IEnumerable<CustomQnAMakerResult> RecalculateScores(IEnumerable<CustomQnAMakerResult> answers)
        {
            //if entities == 0 and there is at least one parametrized answer, then all parametrized answers get down-scored by 0.2
            var isNonParametrizedExist = answers.Any(x => !x.IsAnswerParametrized());
            foreach (var answer in answers.Where(x => x.IsAnswerParametrized()))
            {
                answer.Score = isNonParametrizedExist ? answer.Score - QnAMakerDialogHelper.HighConfidenceDeltaThreshold
                                                      : 0;
            }

            return answers;
        }

        private IEnumerable<String> GetAnswerParameters(String answer)
        {
            var regex = new Regex(_configurationProvider["AnswerParameterRegex"]);
            var result = regex.Matches(answer).Cast<Match>()
                                              .Select(x => x.Groups.Count > 1 ? x.Groups[1].Value : null)
                                              .Where(x => x != null)
                                              .Distinct()
                                              .ToArray();

            return result;
        }
    }
}
