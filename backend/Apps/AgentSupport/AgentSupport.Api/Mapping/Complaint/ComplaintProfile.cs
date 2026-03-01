using AgentSupport.Api.Controllers.Request.Complaint;
using AgentSupport.Api.Controllers.Response.Complaint;
using AgentSupport.Application.UseCases.Complaints.Command;
using AgentSupport.Application.UseCases.Complaints.Dto;
using AutoMapper;

namespace AgentSupport.Api.Mapping.Complaint;

/// <summary>
/// Маппинг для жалоб регается через наследника Profile
/// </summary>
public sealed class ComplaintProfile : Profile
{
    public ComplaintProfile()
    {
        CreateMap<CreateComplaintRequest, CreateComplaintCommand>()
            .ForMember(x => x.Category, opt => opt.Ignore())
            .ForMember(x => x.SuggestedAnswer, opt => opt.Ignore());
        CreateMap<UpdateComplaintRequest, UpdateComplaintCommand>()
            .ForMember(x => x.Id, opt => opt.Ignore());
        CreateMap<ComplaintDto, ComplaintResponse>();
    }
}