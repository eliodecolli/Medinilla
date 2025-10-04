//using Medinilla.App.WebApi.Models;
//using Medinilla.App.WebApi.Models.Base;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Threading.Tasks;
//using System.ComponentModel.DataAnnotations;

//namespace Medinilla.App.WebApi.Controllers
//{
//    [Route("api/{accountId}/charging-stations")]
//    [ApiController]
//    [Produces("application/json")]
//    public class ChargingStationController : ControllerBase
//    {
//        private readonly IChargingStationService _chargingStationService;
//        private readonly ILogger<ChargingStationController> _logger;

//        public ChargingStationController(
//            IChargingStationService chargingStationService,
//            ILogger<ChargingStationController> logger)
//        {
//            _chargingStationService = chargingStationService ?? throw new ArgumentNullException(nameof(chargingStationService));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        /// <summary>
//        /// Retrieves a paginated list of charging stations for the specified account
//        /// </summary>
//        /// <param name="accountId">The account identifier</param>
//        /// <param name="pageNumber">Page number (starts at 1)</param>
//        /// <param name="pageSize">Number of items per page</param>
//        /// <param name="searchTerm">Optional search term to filter results</param>
//        /// <returns>A paginated list of charging stations</returns>
//        /// <response code="200">Returns the list of charging stations</response>
//        /// <response code="400">If the request parameters are invalid</response>
//        /// <response code="404">If the account was not found</response>
//        [HttpGet]
//        [ProducesResponseType(typeof(ApiResult<PaginatedList<ChargingStationApiModel>>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiResult<PaginatedList<ChargingStationApiModel>>), StatusCodes.Status400BadRequest)]
//        [ProducesResponseType(typeof(ApiResult<PaginatedList<ChargingStationApiModel>>), StatusCodes.Status404NotFound)]
//        public async Task<ActionResult<ApiResult<PaginatedList<ChargingStationApiModel>>>> GetChargingStations(
//            [FromRoute] Guid accountId,
//            [FromQuery] int pageNumber = 1,
//            [FromQuery] int pageSize = 10,
//            [FromQuery] string? searchTerm = null)
//        {
//            try
//            {
//                if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
//                {
//                    return BadRequest(new ApiResult<PaginatedList<ChargingStationApiModel>>
//                    {
//                        Error = true,
//                        ErrorMessage = "Invalid pagination parameters"
//                    });
//                }

//                var result = await _chargingStationService.GetChargingStationsAsync(accountId, pageNumber, pageSize, searchTerm);
//                return Ok(new ApiResult<PaginatedList<ChargingStationApiModel>> { Result = result, Error = false });
//            }
//            catch (AccountNotFoundException ex)
//            {
//                _logger.LogWarning(ex, "Account {AccountId} not found", accountId);
//                return NotFound(new ApiResult<PaginatedList<ChargingStationApiModel>>
//                {
//                    Error = true,
//                    ErrorMessage = $"Account with ID {accountId} not found"
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error retrieving charging stations for account {AccountId}", accountId);
//                return StatusCode(500, new ApiResult<PaginatedList<ChargingStationApiModel>>
//                {
//                    Error = true,
//                    ErrorMessage = "An error occurred while processing your request"
//                });
//            }
//        }

//        /// <summary>
//        /// Retrieves a specific charging station by ID
//        /// </summary>
//        /// <param name="accountId">The account identifier</param>
//        /// <param name="id">The charging station identifier</param>
//        /// <returns>The charging station details</returns>
//        /// <response code="200">Returns the charging station</response>
//        /// <response code="404">If the charging station or account was not found</response>
//        [HttpGet("{id}")]
//        [ProducesResponseType(typeof(ApiResult<ChargingStationApiModel>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiResult<ChargingStationApiModel>), StatusCodes.Status404NotFound)]
//        public async Task<ActionResult<ApiResult<ChargingStationApiModel>>> GetChargingStation(
//            [FromRoute] Guid accountId,
//            [FromRoute] Guid id)
//        {
//            try
//            {
//                var result = await _chargingStationService.GetChargingStationByIdAsync(accountId, id);

//                if (result == null)
//                {
//                    return NotFound(new ApiResult<ChargingStationApiModel>
//                    {
//                        Error = true,
//                        ErrorMessage = $"Charging station with ID {id} not found for account {accountId}"
//                    });
//                }

//                return Ok(new ApiResult<ChargingStationApiModel> { Result = result, Error = false });
//            }
//            catch (AccountNotFoundException ex)
//            {
//                _logger.LogWarning(ex, "Account {AccountId} not found", accountId);
//                return NotFound(new ApiResult<ChargingStationApiModel>
//                {
//                    Error = true,
//                    ErrorMessage = $"Account with ID {accountId} not found"
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error retrieving charging station {Id} for account {AccountId}", id, accountId);
//                return StatusCode(500, new ApiResult<ChargingStationApiModel>
//                {
//                    Error = true,
//                    ErrorMessage = "An error occurred while processing your request"
//                });
//            }
//        }

//        /// <summary>
//        /// Updates an existing charging station
//        /// </summary>
//        /// <param name="accountId">The account identifier</param>
//        /// <param name="id">The charging station identifier</param>
//        /// <param name="model">The updated charging station data</param>
//        /// <returns>The updated charging station</returns>
//        /// <response code="200">Returns the updated charging station</response>
//        /// <response code="400">If the model is invalid</response>
//        /// <response code="404">If the charging station or account was not found</response>
//        [HttpPut("{id}")]
//        [ProducesResponseType(typeof(ApiResult<ChargingStationApiModel>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiResult<ChargingStationApiModel>), StatusCodes.Status400BadRequest)]
//        [ProducesResponseType(typeof(ApiResult<ChargingStationApiModel>), StatusCodes.Status404NotFound)]
//        public async Task<ActionResult<ApiResult<ChargingStationApiModel>>> UpdateChargingStation(
//            [FromRoute] Guid accountId,
//            [FromRoute] Guid id,
//            [FromBody] ChargingStationUpdateModel model)
//        {
//            try
//            {
//                if (!ModelState.IsValid)
//                {
//                    return BadRequest(new ApiResult<ChargingStationApiModel>
//                    {
//                        Error = true,
//                        ErrorMessage = "Invalid model state"
//                    });
//                }

//                var result = await _chargingStationService.UpdateChargingStationAsync(accountId, id, model);

//                if (result == null)
//                {
//                    return NotFound(new ApiResult<ChargingStationApiModel>
//                    {
//                        Error = true,
//                        ErrorMessage = $"Charging station with ID {id} not found for account {accountId}"
//                    });
//                }

//                return Ok(new ApiResult<ChargingStationApiModel> { Result = result, Error = false });
//            }
//            catch (AccountNotFoundException ex)
//            {
//                _logger.LogWarning(ex, "Account {AccountId} not found", accountId);
//                return NotFound(new ApiResult<ChargingStationApiModel>
//                {
//                    Error = true,
//                    ErrorMessage = $"Account with ID {accountId} not found"
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error updating charging station {Id} for account {AccountId}", id, accountId);
//                return StatusCode(500, new ApiResult<ChargingStationApiModel>
//                {
//                    Error = true,
//                    ErrorMessage = "An error occurred while processing your request"
//                });
//            }
//        }

//        /// <summary>
//        /// Deletes a charging station
//        /// </summary>
//        /// <param name="accountId">The account identifier</param>
//        /// <param name="id">The charging station identifier</param>
//        /// <returns>No content</returns>
//        /// <response code="204">If the charging station was successfully deleted</response>
//        /// <response code="404">If the charging station or account was not found</response>
//        [HttpDelete("{id}")]
//        [ProducesResponseType(StatusCodes.Status204NoContent)]
//        [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status404NotFound)]
//        public async Task<ActionResult> DeleteChargingStation(
//            [FromRoute] Guid accountId,
//            [FromRoute] Guid id)
//        {
//            try
//            {
//                var success = await _chargingStationService.DeleteChargingStationAsync(accountId, id);

//                if (!success)
//                {
//                    return NotFound(new ApiResult<object>
//                    {
//                        Error = true,
//                        ErrorMessage = $"Charging station with ID {id} not found for account {accountId}"
//                    });
//                }

//                return NoContent();
//            }
//            catch (AccountNotFoundException ex)
//            {
//                _logger.LogWarning(ex, "Account {AccountId} not found", accountId);
//                return NotFound(new ApiResult<object>
//                {
//                    Error = true,
//                    ErrorMessage = $"Account with ID {accountId} not found"
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error deleting charging station {Id} for account {AccountId}", id, accountId);
//                return StatusCode(500, new ApiResult<object>
//                {
//                    Error = true,
//                    ErrorMessage = "An error occurred while processing your request"
//                });
//            }
//        }
//    }
//}