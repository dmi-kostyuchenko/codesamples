using Core.Data.Base;
using Core.Logic.Services.Properties;
using Core.Logic.Services.QnAMaker;
using Microsoft.Bot.Builder.CognitiveServices.QnAMaker;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Logic.Services.Impl
{
    [Serializable]
    public class KnowledgebaseQuestionsProcessingServiceImpl : IKnowledgebaseQuestionsProcessingService
    {
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IAnswersProcessingService _answersProcessingService;
        private readonly IAnswersScoringService _answersScoringService;
        private readonly IDatabasesConnectionService _databasesConnectionService;
        private readonly IQuestionsProcessingService _questionsProcessingService;

        private readonly ICustomQnAService _qnAService;

        public KnowledgebaseQuestionsProcessingServiceImpl(IConfigurationProvider configurationProvider, IAnswersProcessingService answersProcessingService,
                                                           IAnswersScoringService answersScoringService, IDatabasesConnectionService databasesConnectionService,
                                                           IQuestionsProcessingService questionsProcessingService)
        {
            _configurationProvider = configurationProvider;
            _answersProcessingService = answersProcessingService;
            _answersScoringService = answersScoringService;
            _databasesConnectionService = databasesConnectionService;
            _questionsProcessingService = questionsProcessingService;

            _qnAService = new CustomQnAMakerService(new QnAMakerAttribute(_configurationProvider["QnAAuthKey"],
                                                                          _configurationProvider["QnAKnowledgebaseId"],
                                                                          ResourcesDE.NoAnswerOnDialog,
                                                                          _configurationProvider.GetSetting<Double>("AppropriationScoreThreshold"),
                                                                          2,
                                                                          _configurationProvider["QnAEndpointHostName"]));
        }

        /// <summary>
        /// Queries the question from the QnAMaker service KB and provides the answer to the dialog context directly
        /// </summary>
        /// <param name="context">The dialog context</param>
        /// <param name="question">The question text</param>
        /// <returns></returns>
        public async Task QueryTheQuestionAsync(IDialogContext context, String question)
        {
            var qnaResult = await _qnAService.QueryServiceAsync(question);
            //remove answers not matching the entities array
            qnaResult.Answers = _answersScoringService.RemoveAnswersNotMatchingParameters(qnaResult.Answers);
            //recalculate scores by the internal logic
            _answersScoringService.RecalculateScores(qnaResult.Answers);
            //return default answer if we didn't find any
            if (await IsDefaultOrErrorAnswerReturned(question, context, qnaResult, null)) return;

            using (var dbContext = await _databasesConnectionService.GetCurrentUserContext(context))
            {
                if (_answersScoringService.IsConfidentAnswer(qnaResult.Answers))
                {
                    var confidentAnswer = qnaResult.Answers.OrderByDescending(x => x.Score).First();
                    var answer = _answersProcessingService.ProcessConfidentAnswerAndGetResult(dbContext, confidentAnswer);
                    await ReturnAnswer(context, answer);
                }
                else
                {
                    _answersProcessingService.ProcessUnconfidentAnswer(dbContext, qnaResult.Answers);
                    GeneratePrompt(context, qnaResult);
                }
            }
        }

        /// <summary>
        /// Queries the question from the QnAMaker service KB and provides the answer to the dialog context directly
        /// </summary>
        /// <param name="context">The dialog context</param>
        /// <param name="question">The question text</param>
        /// <param name="entities">The LUIS entities</param>
        /// <returns></returns>
        public async Task QueryTheQuestionAsync(IDialogContext context, String question, IEnumerable<EntityRecommendation> entities)
        {
            var processedQuestion = _questionsProcessingService.PreProcessQuestionForKnowledgeBase(question, entities);
            var qnaResult = await _qnAService.QueryServiceAsync(processedQuestion);
            //remove answers not matching the entities array
            qnaResult.Answers = _answersScoringService.RemoveAnswersNotMatchingParameters(qnaResult.Answers, entities);
            //recalculate scores by the internal logic
            _answersScoringService.RecalculateScores(qnaResult.Answers, entities);
            //return default answer if we didn't find any
            if (await IsDefaultOrErrorAnswerReturned(question, context, qnaResult, entities)) return;

            using (var dbContext = await _databasesConnectionService.GetCurrentUserContext(context))
            {
                if (_answersScoringService.IsConfidentAnswer(qnaResult.Answers))
                {
                    var confidentAnswer = qnaResult.Answers.OrderByDescending(x => x.Score).First();
                    var answer = _answersProcessingService.ProcessConfidentAnswerAndGetResult(dbContext, question, confidentAnswer, entities);
                    await ReturnAnswer(context, answer);
                }
                else
                {
                    _answersProcessingService.ProcessUnconfidentAnswer(dbContext, question, qnaResult.Answers, entities);
                    _questionsProcessingService.SubstituteQuestionParameters(question, qnaResult.Answers, entities);

                    GeneratePrompt(context, qnaResult);
                }
            }
        }

        private async Task<Boolean> IsDefaultOrErrorAnswerReturned(String question, IDialogContext context, CustomQnAMakerResults qnaResult, 
                                                                   IEnumerable<EntityRecommendation> entities)
        {
            if (!qnaResult.Answers.Any() || !_answersScoringService.IsAppropriateAnswer(qnaResult.Answers))
            {
                //save unanswered question
                var credentials = await _databasesConnectionService.GetUserCredentials(context);
                _questionsProcessingService.ProcessNotAnsweredQuestion(question, credentials?.UserName);
                //return default answer
                await ReturnAnswer(context, null);
                return true;
            }

            return false;
        }

        private async Task ReturnAnswer(IDialogContext context, String answer)
        {
            if (String.IsNullOrEmpty(answer))
            {
                answer = ResourcesDE.NoAnswerOnDialog;
            }

            await context.PostAsync(answer);
        }

        private void GeneratePrompt(IDialogContext context, CustomQnAMakerResults qnaResult)
        {
            var helper = GetDialogHelper(context, qnaResult);
            var qnaList = qnaResult.Answers;
            var questions = qnaList.Select(x => x.Questions[0])
                                   .Concat(new[] { ResourcesDE.NoneOfTheAboveOption }).ToArray();

            PromptOptions<string> promptOptions = new PromptOptions<string>(
                prompt: ResourcesDE.AnswerSelectionPrompt,
                tooManyAttempts: ResourcesDE.TooManyAttempts,
                options: questions,
                attempts: 0);

            PromptDialog.Choice(context: context, resume: helper.ResumeAndPostAnswer, promptOptions: promptOptions);
        }

        private QnAMakerDialogHelper GetDialogHelper(IDialogContext context, CustomQnAMakerResults qnaResult)
        {
            var message = context.Activity.AsMessageActivity();
            FeedbackRecord feedback = null;
            if (message != null)
            {
                feedback = new FeedbackRecord
                {
                    UserId = message.From.Id,
                    UserQuestion = message.Text
                };
            }
            return new QnAMakerDialogHelper(qnaResult, feedback, _qnAService);
        }
    }
}
