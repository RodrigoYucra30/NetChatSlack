using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using AutoMapper;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Channels
{
    public class PrivateChannelDetails
    {
        public class Query : IRequest<ChannelDto>
        {
            public string UserId { get; set; }
        }

        public class Handler : IRequestHandler<Query, ChannelDto>
        {
            private readonly DataContext _context;

            private readonly IMapper _mapper;

            private readonly IUserAccessor _userAccessor;

            public Handler(
                DataContext context,
                IMapper mapper,
                IUserAccessor userAccessor
            )
            {
                _context = context;
                _mapper = mapper;
                _userAccessor = userAccessor;
            }

            public async Task<ChannelDto>
            Handle(Query request, CancellationToken cancellationToken)
            {
                var currentUser =
                    await _context
                        .Users
                        .SingleOrDefaultAsync(x =>
                            x.UserName == _userAccessor.GetCurrentUserName());

                var user = await _context.Users.FindAsync(request.UserId);

                var privateChannelIdForCurrentUser =
                    GetPrivateChannelId(currentUser.Id.ToString(),
                    request.UserId);
                var privateChannelIdForRecipientUser =
                    GetPrivateChannelId(request.UserId,
                    currentUser.Id.ToString());

                var channel =
                    await _context
                        .Channels
                        .Include(x => x.Messages)
                        .ThenInclude(x => x.Sender)
                        .SingleOrDefaultAsync(x =>
                            x.PrivateChannelId ==
                            privateChannelIdForCurrentUser ||
                            x.PrivateChannelId ==
                            privateChannelIdForRecipientUser);

                if (channel == null)
                {
                    var newChannel =
                        new Channel {
                            Id = Guid.NewGuid(),
                            Name = currentUser.UserName,
                            Description = user.UserName,
                            ChannelType = ChannelType.Room,
                            PrivateChannelId = privateChannelIdForCurrentUser
                        };

                    _context.Channels.Add (newChannel);

                    var success = await _context.SaveChangesAsync() > 0;

                    if (success)
                    {
                        return _mapper.Map<Channel, ChannelDto>(newChannel);
                    }
                }

                var channelToReturn = _mapper.Map<Channel, ChannelDto>(channel);
                return channelToReturn;
            }

            private string
            GetPrivateChannelId(string currentUserId, string userId) =>
                $"{currentUserId}/{userId}";
        }
    }
}
