using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using Politiq.Pollies.Domain.Parties.Queries;
using Politiq.Pollies.Domain.Pollies.Queries;
using Politiq.Social.Domain.Tags;
using Politiq.Social.Domain.Tags.Commands;
using Politiq.Social.Domain.Tags.Enums;
using Politiq.Social.Domain.Tags.Queries;
using Politiq.Social.Services.Dto;
using Politiq.Social.Services.Interfaces;

namespace Politiq.Social.Services
{
    public class TagsService : ITagsService
    {
        private readonly IMediator _mediator;

        public TagsService(IMediator mediator) => _mediator = mediator;

        public async Task<IEnumerable<TagState>> GetOrCreateTags(IEnumerable<TagDto> tags)
        {
            if (tags == null || !tags.Any()) return null;

            var tagIds = tags.Select(x =>
            {
                Guid.TryParse(x.Id, out Guid id); //we do not fail here because tags are not critical values
                return id;
            }).Where(x => x != default(Guid)).ToArray();

            var query = new GetTagsByIds(tagIds);
            var result = new List<TagState>();
            result.AddRange(await _mediator.Send(query));

            var newTagsCommand = new AddTags(tags.Where(x => string.IsNullOrEmpty(x.Id) && x.Type == TagType.Text).Select(x => x.Name).ToArray());
            if (newTagsCommand.TagNames != null && newTagsCommand.TagNames.Any())
                result.AddRange(await _mediator.Send(newTagsCommand));

            return result;
        }

        public async Task<IEnumerable<TagExtendedDto>> GetExtendedTags(IEnumerable<TagState> tags)
        {
            if (tags == null || !tags.Any()) return Array.Empty<TagExtendedDto>();

            var extended = tags.Where(x => x.Type == TagType.Text).Select(x => new TagExtendedDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type
            }).ToList();

            extended.AddRange(await GetPollyTags(tags));

            extended.AddRange(await GetPartyTags(tags));

            return extended;
        }

        private async Task<IEnumerable<PollyTagExtendedDto>> GetPollyTags(IEnumerable<TagState> tags)
        {
            var pollyTags = tags.Where(x => x.Type == TagType.Polly && x.EntityId != null);
            var polliesQuery = new GetAggregatedPolliesBySetOfIds(pollyTags.Select(x =>
            {
                int.TryParse(x.EntityId, out int personID);
                return personID;
            }).Distinct().ToArray());

            var pollies = polliesQuery.PersonIDs.Any() ? await _mediator.Send(polliesQuery) : null;

            if (pollies == null || !pollies.Any()) return Array.Empty<PollyTagExtendedDto>();

            return pollyTags.Where(x => pollies.Any(y => y.PersonID.ToString() == x.EntityId))
                    .Select(x =>
                    {
                        var polly = pollies.First(y => y.PersonID.ToString() == x.EntityId);
                        return new PollyTagExtendedDto
                        {
                            Id = x.Id,
                            Name = x.Name,
                            Type = x.Type,

                            DisplayName = polly.DisplayName,
                            LastOfficeTitle = polly.LastOfficeTitle,
                            PersonID = polly.PersonID,
                            PhotoName = polly.PhotoName,
                            URLSlug = polly.URLSlug
                        };
                    }).ToArray();
        }

        private async Task<IEnumerable<PartyTagExtendedDto>> GetPartyTags(IEnumerable<TagState> tags)
        {
            var partyTags = tags.Where(x => x.Type == TagType.Party && x.EntityId != null);
            var partiesQuery = new GetPartiesBySetOfIds(partyTags.Select(x =>
            {
                int.TryParse(x.EntityId, out int personID);
                return personID;
            }).Distinct().ToArray());

            var parties = partiesQuery.PartyIds.Any() ? await _mediator.Send(partiesQuery) : null;
            if (parties == null || !parties.Any()) return Array.Empty<PartyTagExtendedDto>();

            return partyTags.Where(x => parties.Any(y => y.ID.ToString() == x.EntityId))
                    .Select(x =>
                    {
                        var party = parties.First(y => y.ID.ToString() == x.EntityId);
                        return new PartyTagExtendedDto
                        {
                            Id = x.Id,
                            Name = x.Name,
                            Type = x.Type,

                            DisplayName = party.Name,
                            NameAbbreviated = party.NameAbbreviated,
                            PartyId = party.ID,
                            PhotoName = party.PhotoName,
                            URLSlug = party.URLSlug
                        };
                    }).ToArray();
        }
    }
}
