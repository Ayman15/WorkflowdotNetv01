//// WorkflowServer/Controllers/ApprovalController.cs
//using System;
//using System.ComponentModel.DataAnnotations;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
////using WorkflowLib; // <- Namespace where ApprovalService & WorkflowInit are defined

//namespace WorkflowServer.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class ApprovalController : ControllerBase
//    {
//        private readonly ILogger<ApprovalController> _logger;
//        private readonly IConfiguration _config;

//        // Read from appsettings.json:
//        // "ApprovalRunner": { "ExePath": "C:\\Tools\\YourExe.exe", "Arguments": "--flag1 --flag2" }
//        private readonly string _exePath;
//        private readonly string _exeArgs;

//        public ApprovalController(ILogger<ApprovalController> logger, IConfiguration config)
//        {
//            _logger = logger;
//            _config = config;

//            _exePath = _config["ApprovalRunner:ExePath"] ?? "";
//            _exeArgs = _config["ApprovalRunner:Arguments"] ?? "";
//        }

//        /// <summary>
//        /// Starts a new workflow instance and returns approval links you can drop in an email.
//        /// POST /api/approval/start
//        /// </summary>
//        [HttpPost("start")]
//        public async Task<IActionResult> Start([FromBody] StartRequest req)
//        {
//            if (string.IsNullOrWhiteSpace(req.ApproverEmails))
//                return BadRequest("ApproverEmails is required.");

//            // Who is starting the workflow (for audit); fall back to a fixed string
//            var starterIdentity = HttpContext?.User?.Identity?.Name ?? "starter";

//            // Create the instance and set initial parameters via ApprovalService
//            var pid = await ApprovalService.StartAsync(
//                approverEmails: req.ApproverEmails,
//                subject: req.Subject ?? "(no subject)",
//                body: req.Body ?? "",
//                wellsJson: req.WellsJson ?? "[]",
//                approvalToken: req.ApprovalToken ?? Guid.NewGuid().ToString("N"),
//                identity: starterIdentity
//            );

//            // Build approve/reject links that your email can include
//            var baseUrl = $"{Request.Scheme}://{Request.Host}";
//            var approveUrl = $"{baseUrl}/api/approval/approve?pid={pid}&token={Uri.EscapeDataString(req.ApprovalToken ?? "")}";
//            var rejectUrl = $"{baseUrl}/api/approval/reject?pid={pid}&token={Uri.EscapeDataString(req.ApprovalToken ?? "")}";

//            return Ok(new
//            {
//                processId = pid,
//                approveUrl,
//                rejectUrl
//            });
//        }

//        /// <summary>
//        /// Approve: runs EXE first, then sends Approve command (workflow goes Approved -> Final automatically).
//        /// GET /api/approval/approve?pid=...&token=...
//        /// </summary>
//        [HttpGet("approve")]
//        public async Task<IActionResult> Approve([FromQuery][Required] Guid pid, [FromQuery][Required] string token)
//        {
//            if (!ValidateToken(pid, token))
//                return Unauthorized("Invalid or expired token.");

//            if (string.IsNullOrWhiteSpace(_exePath))
//                return StatusCode(500, "ApprovalRunner:ExePath not configured in appsettings.json.");

//            var approverIdentity = HttpContext?.User?.Identity?.Name ?? "approver";

//            try
//            {
//                await ApprovalService.ApproveAndRunAsync(
//                    processId: pid,
//                    approverIdentity: approverIdentity,
//                    exePath: _exePath,
//                    arguments: _exeArgs
//                );

//                return Ok(new
//                {
//                    message = "Approved and executed. Workflow ended (Approved -> Final).",
//                    processId = pid
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error approving workflow {Pid}", pid);
//                return StatusCode(500, $"Error while approving: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Reject: sends Reject command (workflow goes Rejected -> Final automatically).
//        /// GET /api/approval/reject?pid=...&token=...
//        /// </summary>
//        [HttpGet("reject")]
//        public async Task<IActionResult> Reject([FromQuery][Required] Guid pid, [FromQuery][Required] string token)
//        {
//            if (!ValidateToken(pid, token))
//                return Unauthorized("Invalid or expired token.");

//            var approverIdentity = HttpContext?.User?.Identity?.Name ?? "approver";
//            try
//            {
//                await ApprovalService.RejectAsync(pid, approverIdentity, "Rejected by approver");
//                return Ok(new
//                {
//                    message = "Rejected. Workflow ended (Rejected -> Final).",
//                    processId = pid
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error rejecting workflow {Pid}", pid);
//                return StatusCode(500, $"Error while rejecting: {ex.Message}");
//            }
//        }

//        [HttpGet("approve-remote-agent")]
//        public async Task<IActionResult> ApproveRemoteAgent([FromQuery] Guid pid, [FromQuery] string token)
//        {
//            if (!ValidateToken(pid, token)) return Unauthorized();

//            var agentUrl = _config["RemoteAgent:BaseUrl"];   // e.g., "http://SERVER01:5000"
//            var apiKey = _config["RemoteAgent:ApiKey"];    // must match the agent
//            var exe = _config["RemoteAgent:ExePath"];   // must be whitelisted on remote
//            var args = _config["RemoteAgent:Arguments"]; // e.g., "C:\\Users\\Public\\test.xlsx"

//            await ApprovalService.ApproveAndRunViaAgentAsync(
//                processId: pid,
//                approverIdentity: HttpContext?.User?.Identity?.Name ?? "approver",
//                agentBaseUrl: agentUrl,
//                apiKey: apiKey,
//                exePath: exe,
//                arguments: args
//            );

//            return Ok(new { message = "Approved & executed via remote agent. Workflow ended.", processId = pid });
//        }


//        // ---------------- helpers ----------------

//        // TODO: Replace with your real token store/validation
//        private bool ValidateToken(Guid processId, string token)
//        {
//            // For now: accept nonempty token. You likely stored the token as a workflow parameter
//            // when you created the instance. If you want to enforce it, you can retrieve the
//            // saved token and compare here (e.g., via your own store).
//            return !string.IsNullOrWhiteSpace(token);
//        }
//    }

//    public class StartRequest
//    {
//        [Required]
//        public string ApproverEmails { get; set; }

//        public string Subject { get; set; }
//        public string Body { get; set; }

//        /// <summary>
//        /// JSON array/string describing wells; pass through to the workflow parameter.
//        /// </summary>
//        public string WellsJson { get; set; }

//        /// <summary>
//        /// Optional. If omitted, server will generate a new token.
//        /// </summary>
//        public string ApprovalToken { get; set; }
//    }
//}
