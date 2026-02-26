using AgentSupport.Api.Controllers.Request.Complaint;
using AgentSupport.Api.Controllers.Response.Complaint;
using AgentSupport.Application.UseCases.Complaints.Command;
using AgentSupport.Application.UseCases.Complaints.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace AgentSupport.Api.Controllers;

/// <summary>
/// Агент контроллер жалоб
/// </summary>
[ApiController]
[Route("api/v1/agent")]
public sealed class AgentController : ControllerBase
{
    private readonly ICreateComplaintUseCase _createComplaintUseCase;
    private readonly IGetComplaintByIdUseCase _getComplaintByIdUseCase;
    private readonly IGetComplaintsUseCase _getComplaintsUseCase;
    private readonly IUpdateComplaintUseCase _updateComplaintUseCase;
    private readonly IDeleteComplaintUseCase _deleteComplaintUseCase;
    private readonly IMapper _mapper;

    public AgentController(
        ICreateComplaintUseCase createComplaintUseCase,
        IGetComplaintByIdUseCase getComplaintByIdUseCase,
        IGetComplaintsUseCase getComplaintsUseCase,
        IUpdateComplaintUseCase updateComplaintUseCase,
        IDeleteComplaintUseCase deleteComplaintUseCase,
        IMapper mapper)
    {
        _createComplaintUseCase = createComplaintUseCase;
        _getComplaintByIdUseCase = getComplaintByIdUseCase;
        _getComplaintsUseCase = getComplaintsUseCase;
        _updateComplaintUseCase = updateComplaintUseCase;
        _deleteComplaintUseCase = deleteComplaintUseCase;
        _mapper = mapper;
    }

    /// <summary>
    /// Создать жалобу
    /// </summary>
    [HttpPost("complaints")]
    [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ComplaintResponse>> CreateComplaint(
        [FromBody] CreateComplaintRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "Запрос не может быть null");
        }

        var command = _mapper.Map<CreateComplaintCommand>(request);

        var dto = await _createComplaintUseCase.CreateAsync(command, cancellationToken);

        var response = _mapper.Map<ComplaintResponse>(dto);

        return CreatedAtAction(nameof(GetComplaintById), new
        {
            id = response.Id,
        }, response);
    }

    /// <summary>
    /// Получить жалобу
    /// </summary>
    [HttpGet("complaints/{id:guid}")]
    [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplaintResponse>> GetComplaintById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var dto = await _getComplaintByIdUseCase.GetByIdAsync(id, cancellationToken);

        var response = _mapper.Map<ComplaintResponse>(dto);

        return Ok(response);
    }

    /// <summary>
    /// Получить жалобы
    /// </summary>
    [HttpGet("complaints")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ComplaintResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ComplaintResponse>>> GetComplaints(CancellationToken cancellationToken)
    {
        var dtos = await _getComplaintsUseCase.GetAllAsync(cancellationToken);

        var response = dtos.Select(x => _mapper.Map<ComplaintResponse>(x)).ToArray();

        return Ok(response);
    }

    /// <summary>
    /// Обновление жалобы
    /// </summary>
    [HttpPut("complaints/{id:guid}")]
    [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplaintResponse>> UpdateComplaint([FromRoute] Guid id, [FromBody] UpdateComplaintRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "Запрос не может быть null");
        }

        var command = _mapper.Map<UpdateComplaintCommand>(request);
        command.Id = id;

        var dto = await _updateComplaintUseCase.UpdateAsync(command, cancellationToken);

        var response = _mapper.Map<ComplaintResponse>(dto);

        return Ok(response);
    }

    /// <summary>
    /// Удаление жалобы
    /// </summary>
    [HttpDelete("complaints/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteComplaint([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await _deleteComplaintUseCase.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}