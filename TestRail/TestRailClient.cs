using EnumStringValues;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using TestRail.Enums;
using TestRail.Types;
using TestRail.Utils;

namespace TestRail
{
    /// <summary>client used to access test case data in testrail</summary>
    public class TestRailClient
    {
        /// <summary>url for testrail</summary>
        protected string Url;
        /// <summary>base 64 string of the given credentials</summary>
        protected string AuthInfo;

        /// <summary>projects in the test rail database</summary>
        public List<Project> Projects => _projects.Value;

        /// <summary>called when the client sends an http request</summary>
        public event EventHandler<HttpRequestSentEventArgs> OnHttpRequestSent = (s, e) => { };

        /// <summary>called when the client receives an http response</summary>
        public event EventHandler<string> OnHttpResponseReceived = (s, e) => { };

        /// <summary>called when an operation fails</summary>
        public event EventHandler<string> OnOperationFailed = (s, e) => { };

        /// <inheritdoc />
        /// <summary>event args for http request sent</summary>
        public class HttpRequestSentEventArgs : EventArgs
        {
            /// <summary>http method (GET, POST, PUT, DELETE, etc.)</summary>
            public string Method;

            /// <summary>uri</summary>
            public Uri Uri;

            /// <summary>post data</summary>
            public string PostContent;

            /// <inheritdoc />
            /// <summary>constructor</summary>
            /// <param name="method">http method used</param>
            /// <param name="uri">uri used</param>
            /// <param name="postContent">post content sent (if any)</param>
            public HttpRequestSentEventArgs(string method, Uri uri, string postContent = null)
            {
                Method = method;
                Uri = uri;
                PostContent = postContent;
            }
        }

        /// <summary>list of projects in the current testrail instance</summary>
        private readonly Lazy<List<Project>> _projects;

        /// <summary>dictionary of priority ID (from test rail) to priority levels(where Higher value means higher priority)</summary>
        private Dictionary<ulong, int> PriorityIdToLevel => LazyPriorityIdToLevel.Value;

        /// <summary>dictionary of priority ID (from test rail) to priority levels(where Higher value means higher priority)</summary>
        private Lazy<Dictionary<ulong, int>> LazyPriorityIdToLevel { get; }

        #region Constructor
        /// <summary>constructor</summary>
        /// <param name="url">url for test rail</param>
        /// <param name="username">user name</param>
        /// <param name="password">password</param>
        public TestRailClient(string url, string username, string password)
        {
            Url = url;
            AuthInfo = Convert.ToBase64String(Encoding.Default.GetBytes($"{username}:{password}"));

            _projects = new Lazy<List<Project>>(InternalGetProjects);

            // set up the lazy loading of the priority dictionary (priority id to priority value)
            LazyPriorityIdToLevel = new Lazy<Dictionary<ulong, int>>(_CreatePrioritiesDict);
        }
        #endregion Constructor

        #region Public Methods
        /// <summary>Get the priority for the case if we can</summary>
        /// <param name="c">case to get the priority from</param>
        /// <returns>int value of priority if possible, null if not found</returns>
        public int? GetPriorityForCase(Case c)
        {
            int? priority = null;

            if (null != c?.PriorityId && null != PriorityIdToLevel && PriorityIdToLevel.ContainsKey(c.PriorityId.Value))
            {
                priority = PriorityIdToLevel[c.PriorityId.Value];
            }

            return priority;
        }

        #region Add Commands
        /// <summary>
        /// Adds a new test result, comment or assigns a test.
        /// It's recommended to use AddResults() instead if you plan to add results for multiple tests.
        /// </summary>
        /// <param name="testId">The ID of the test the result should be added to.</param>
        /// <param name="status">The test status.</param>
        /// <param name="comment">The comment/description for the test result.</param>
        /// <param name="version">The version or build you tested against.</param>
        /// <param name="elapsed">The time it took to execute the test, e.g. "30s" or "1m 45s".</param>
        /// <param name="defects">A comma-separated list of defects to link to the test result.</param>
        /// <param name="assignedToId">The ID of a user the test should be assigned to.</param>
        /// <param name="customs">Custom fields are supported as well and must be submitted with their system name, prefixed with 'custom_', e.g. custom_comment</param>
        /// <returns>If successful, this method will return the new test result.</returns>
        public RequestResult<Result> AddResult(ulong testId, ResultStatus? status, string comment = null,
            string version = null, TimeSpan? elapsed = null, string defects = null, ulong? assignedToId = null, JObject customs = null)
        {
            var uri = _CreateUri_(CommandType.Add, CommandAction.Result, testId);

            var result = new Result
            {
                TestId = testId,
                StatusId = (ulong?)status,
                Comment = comment,
                Version = version,
                Elapsed = elapsed,
                Defects = defects,
                AssignedToId = assignedToId
            };

            var jsonParams = JsonUtility.Merge(result.GetJson(), customs);

            return SendPostCommand<Result>(uri, jsonParams);
        }

        /// <summary>
        /// Adds a new test result, comment or assigns a test.
        /// It's recommended to use AddResultsForCases() instead if you plan to add results for multiple test cases.
        /// </summary>
        /// <param name="runId">The ID of the test run.</param>
        /// <param name="caseId">The ID of the test case.</param>
        /// <param name="status">The test status.</param>
        /// <param name="comment">The comment/description for the test result.</param>
        /// <param name="version">The version or build you tested against.</param>
        /// <param name="elapsed">The time it took to execute the test, e.g. "30s" or "1m 45s".</param>
        /// <param name="defects">A comma-separated list of defects to link to the test result.</param>
        /// <param name="assignedToId">The ID of a user the test should be assigned to.</param>
        /// <param name="customs">Custom fields are supported as well and must be submitted with their system name, prefixed with 'custom_', e.g. custom_comment</param>
        /// <returns>If successful, this method will return the new test result.</returns>
        public RequestResult<Result> AddResultForCase(ulong runId, ulong caseId, ResultStatus? status, string comment = null,
            string version = null, TimeSpan? elapsed = null, string defects = null, ulong? assignedToId = null, JObject customs = null)
        {
            var uri = _CreateUri_(CommandType.Add, CommandAction.ResultForCase, runId, caseId);

            var result = new Result
            {
                StatusId = (ulong?)status,
                Comment = comment,
                Version = version,
                Elapsed = elapsed,
                Defects = defects,
                AssignedToId = assignedToId
            };

            var jsonParams = JsonUtility.Merge(result.GetJson(), customs);

            return SendPostCommand<Result>(uri, jsonParams);
        }

        // TODO: - Add a method called AddResultsForCases()
        // http://docs.gurock.com/testrail-api2/reference-results#add_results_for_cases

        /// <summary>Creates a new test run.</summary>
        /// <param name="projectId">The ID of the project the test run should be added to.</param>
        /// <param name="suiteId">The ID of the test suite for the test run (optional if the project is operating in single suite mode, required otherwise).</param>
        /// <param name="name">	The name of the test run.</param>
        /// <param name="description">The description of the test run.</param>
        /// <param name="milestoneId">The ID of the milestone to link to the test run.</param>
        /// <param name="assignedToId">The ID of the user the test run should be assigned to.</param>
        /// <param name="caseIds">An array of case IDs for the custom case selection.</param>
        /// <param name="customs">Custom fields are supported as well and must be submitted with their system name, prefixed with 'custom_', e.g. custom_comment</param>
        /// <returns>If successful, this method returns the new test run.</returns>
        public RequestResult<Run> AddRun(ulong projectId, ulong suiteId, string name, string description, ulong milestoneId,
            ulong? assignedToId = null, HashSet<ulong> caseIds = null, JObject customs = null)
        {
            var includeAll = true;

            // validates whether we are in include all or custom case selection mode
            if (null != caseIds)
            {
                var atLeastOneCaseFoundInSuite = _CasesFoundInSuite(projectId, suiteId, caseIds);

                if (atLeastOneCaseFoundInSuite)
                {
                    includeAll = false;
                }

                else
                {
                    return new RequestResult<Run>(HttpStatusCode.BadRequest, thrownException: new Exception("Case ids not found in the Suite"));
                }
            }

            var uri = _CreateUri_(CommandType.Add, CommandAction.Run, projectId);

            var run = new Run
            {
                SuiteId = suiteId,
                Name = name,
                Description = description,
                MilestoneId = milestoneId,
                AssignedTo = assignedToId,
                IncludeAll = includeAll,
                CaseIds = caseIds
            };

            var jsonParams = JsonUtility.Merge(run.GetJson(), customs);

            return SendPostCommand<Run>(uri, jsonParams);
        }

        /// <summary>Creates a new test case.</summary>
        /// <param name="sectionId">The ID of the section the test case should be added to.</param>
        /// <param name="title">The title of the test case (required).</param>
        /// <param name="typeId">The ID of the case type.</param>
        /// <param name="priorityId">The ID of the case priority.</param>
        /// <param name="estimate">The estimate, e.g. "30s" or "1m 45s".</param>
        /// <param name="milestoneId">The ID of the milestone to link to the test case.</param>
        /// <param name="refs">A comma-separated list of references/requirements.</param>
        /// <param name="customFields">Custom fields are supported as well and must be submitted with their system name, prefixed with 'custom_', e.g. custom_preconds</param>
        /// <returns>If successful, this method returns the new test case.</returns>
        public RequestResult<Case> AddCase(ulong sectionId, string title, ulong? typeId = null, ulong? priorityId = null,
            string estimate = null, ulong? milestoneId = null, string refs = null, JObject customFields = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return new RequestResult<Case>(HttpStatusCode.BadRequest, thrownException: new ArgumentNullException(nameof(title)));
            }

            var uri = _CreateUri_(CommandType.Add, CommandAction.Case, sectionId);

            var tmpCase = new Case
            {
                Title = title,
                TypeId = typeId,
                PriorityId = priorityId,
                Estimate = estimate,
                MilestoneId = milestoneId,
                References = refs
            };

            var jsonParams = JsonUtility.Merge(tmpCase.GetJson(), customFields);

            return SendPostCommand<Case>(uri, jsonParams);
        }

        /// <summary>Creates a new project (admin status required).</summary>
        /// <param name="projectName">The name of the project (required).</param>
        /// <param name="announcement">The description of the project.</param>
        /// <param name="showAnnouncement">True if the announcement should be displayed on the project's overview page and false otherwise.</param>
        /// <returns>If successful, this method returns the new project.</returns>
        public RequestResult<Project> AddProject(string projectName, string announcement = null, bool? showAnnouncement = null)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return new RequestResult<Project>(HttpStatusCode.BadRequest, thrownException: new ArgumentNullException(nameof(projectName)));
            }

            var uri = _CreateUri_(CommandType.Add, CommandAction.Project);

            var project = new Project
            {
                Name = projectName,
                Announcement = announcement,
                ShowAnnouncement = showAnnouncement
            };

            return SendPostCommand<Project>(uri, project.GetJson());
        }

        /// <summary>Creates a new section.</summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <param name="suiteId">The ID of the test suite (ignored if the project is operating in single suite mode, required otherwise).</param>
        /// <param name="name">The name of the section (required).</param>
        /// <param name="parentId">The ID of the parent section (to build section hierarchies).</param>
        /// <param name="description">The description of the section (added with TestRail 4.0).</param>
        /// <returns>If successful, this method returns the new section.</returns>
        public RequestResult<Section> AddSection(ulong projectId, ulong suiteId, string name, ulong? parentId = null, string description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new RequestResult<Section>(HttpStatusCode.BadRequest, thrownException: new ArgumentNullException(nameof(name)));
            }

            var uri = _CreateUri_(CommandType.Add, CommandAction.Section, projectId);

            var section = new Section
            {
                SuiteId = suiteId,
                ParentId = parentId,
                Name = name,
                Description = description
            };

            return SendPostCommand<Section>(uri, section.GetJson());
        }

        /// <summary>Creates a new test suite.</summary>
        /// <param name="projectId">The ID of the project the test suite should be added to.</param>
        /// <param name="name">The name of the test suite (required).</param>
        /// <param name="description">The description of the test suite.</param>
        /// <returns>If successful, this method returns the new test suite.</returns>
        public RequestResult<Suite> AddSuite(ulong projectId, string name, string description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new RequestResult<Suite>(HttpStatusCode.BadRequest, thrownException: new ArgumentNullException(nameof(name)));
            }

            var uri = _CreateUri_(CommandType.Add, CommandAction.Suite, projectId);

            var suite = new Suite
            {
                Name = name,
                Description = description
            };

            return SendPostCommand<Suite>(uri, suite.GetJson());
        }

        /// <summary>Creates a new test plan.</summary>
        /// <param name="projectId">The ID of the project the test plan should be added to.</param>
        /// <param name="name">The name of the test plan (required).</param>
        /// <param name="description">The description of the test plan.</param>
        /// <param name="milestoneId">The ID of the milestone to link to the test plan.</param>
        /// <param name="entries">An array of objects describing the test runs of the plan.</param>
        /// <param name="customs">Custom fields are supported as well and must be submitted with their system name, prefixed with 'custom_', e.g. custom_comment</param>
        /// <returns>If successful, this method returns the new test plan.</returns>
        public RequestResult<Plan> AddPlan(ulong projectId, string name, string description = null, ulong? milestoneId = null,
            List<PlanEntry> entries = null, JObject customs = null)// TODO: - Add config ids here: , params ulong[] suiteIDs)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new RequestResult<Plan>(HttpStatusCode.BadRequest, thrownException: new ArgumentNullException(nameof(name)));
            }

            var uri = _CreateUri_(CommandType.Add, CommandAction.Plan, projectId);

            var plan = new Plan
            {
                Name = name,
                Description = description,
                MilestoneId = milestoneId,
                Entries = entries
            };

            var jsonParams = JsonUtility.Merge(plan.GetJson(), customs);

            return SendPostCommand<Plan>(uri, jsonParams);
        }

        /// <summary>Adds one or more new test runs to a test plan.</summary>
        /// <param name="planId">The ID of the plan the test runs should be added to.</param>
        /// <param name="suiteId">The ID of the test suite for the test run(s) (required).</param>
        /// <param name="name">The name of the test run(s).</param>
        /// <param name="assignedToId">The ID of the user the test run(s) should be assigned to.</param>
        /// <param name="caseIds">An array of case IDs for the custom case selection.</param>
        /// <param name="customs">Custom fields are supported as well and must be submitted with their system name, prefixed with 'custom_', e.g. custom_comment</param>
        /// <returns>If successful, this method returns the new test plan entry including test runs.</returns>
        public RequestResult<PlanEntry> AddPlanEntry(ulong planId, ulong suiteId, string name = null, ulong? assignedToId = null,
            List<ulong> caseIds = null, JObject customs = null)
        {
            var uri = _CreateUri_(CommandType.Add, CommandAction.PlanEntry, planId);

            var planEntry = new PlanEntry
            {
                AssignedToId = assignedToId,
                SuiteId = suiteId,
                Name = name,
                CaseIds = caseIds
            };

            var jsonParams = JsonUtility.Merge(planEntry.GetJson(), customs);

            return SendPostCommand<PlanEntry>(uri, jsonParams);
        }

        /// <summary>Creates a new milestone.</summary>
        /// <param name="projectId">The ID of the project the milestone should be added to.</param>
        /// <param name="name">The name of the milestone (required).</param>
        /// <param name="description">The description of the milestone.</param>
        /// <param name="parentId">The ID of the parent milestone, if any (for sub-milestones) (available since TestRail 5.3).</param> 
        /// <param name="dueOn">The due date of the milestone (as UNIX timestamp).</param>
        /// <returns>If successful, this method returns the new milestone.</returns>
        public RequestResult<Milestone> AddMilestone(ulong projectId, string name, string description = null, ulong? parentId = null, DateTime? dueOn = null)
        {
            var uri = _CreateUri_(CommandType.Add, CommandAction.Milestone, projectId);

            var milestone = new Milestone
            {
                Name = name,
                Description = description,
                DueOn = dueOn,
                ParentId = parentId
            };

            return SendPostCommand<Milestone>(uri, milestone.GetJson());
        }
        #endregion Add Commands

        #region Update Commands
        /// <summary>Updates an existing test case (partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="caseId">The ID of the test case.</param>
        /// <param name="title">The title of the test case.</param>
        /// <param name="typeId">The ID of the test case type that is linked to the test case.</param>
        /// <param name="priorityId">The ID of the priority that is linked to the test case.</param>
        /// <param name="estimate">The estimate, e.g. "30s" or "1m 45s".</param>
        /// <param name="milestoneId">The ID of the milestone that is linked to the test case.</param>
        /// <param name="refs">A comma-separated list of references/requirements.</param>
        /// <param name="customs">Custom fields are also included in the response and use their system name prefixed with 'custom_' as their field identifier.</param>
        /// <returns>If successful, this method returns the updated test case.</returns>
        public RequestResult<Case> UpdateCase(ulong caseId, string title, ulong? typeId = null, ulong? priorityId = null, string estimate = null,
            ulong? milestoneId = null, string refs = null, JObject customs = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return new RequestResult<Case>(HttpStatusCode.BadRequest, thrownException: new ArgumentNullException(nameof(title)));
            }

            var uri = _CreateUri_(CommandType.Update, CommandAction.Case, caseId);

            var tmpCase = new Case
            {
                Title = title,
                TypeId = typeId,
                PriorityId = priorityId,
                Estimate = estimate,
                MilestoneId = milestoneId,
                References = refs
            };

            var jsonParams = JsonUtility.Merge(tmpCase.GetJson(), customs);

            return SendPostCommand<Case>(uri, jsonParams);
        }

        /// <summary>Updates an existing milestone (partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="milestoneId">The ID of the milestone.</param>
        /// <param name="name">The name of the milestone (required).</param>
        /// <param name="description">The description of the milestone.</param>
        /// <param name="dueOn">The due date of the milestone (as UNIX timestamp).</param>
        /// <param name="isCompleted">True if a milestone is considered completed and false otherwise.</param>
        /// <returns>If successful, this method returns the updated milestone.</returns>
        public RequestResult<Milestone> UpdateMilestone(ulong milestoneId, string name = null, string description = null, DateTime? dueOn = null, bool? isCompleted = null)
        {
            var uri = _CreateUri_(CommandType.Update, CommandAction.Milestone, milestoneId);

            var milestone = new Milestone
            {
                Name = name,
                Description = description,
                DueOn = dueOn,
                IsCompleted = isCompleted
            };

            return SendPostCommand<Milestone>(uri, milestone.GetJson());
        }

        /// <summary>Updates an existing test plan (partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="planId">The ID of the test plan.</param>
        /// <param name="name">The name of the test plan (required).</param>
        /// <param name="description">The description of the test plan.</param>
        /// <param name="milestoneId">The ID of the milestone to link to the test plan.</param>
        /// <param name="customs">Custom fields are also included in the response and use their system name prefixed with 'custom_' as their field identifier.</param>
        /// <returns>If successful, this method returns the updated test plan.</returns>
        public RequestResult<Plan> UpdatePlan(ulong planId, string name = null, string description = null, ulong? milestoneId = null, JObject customs = null)
        {
            var uri = _CreateUri_(CommandType.Update, CommandAction.Plan, planId);

            var plan = new Plan
            {
                Name = name,
                Description = description,
                MilestoneId = milestoneId
            };

            var jsonParams = JsonUtility.Merge(plan.GetJson(), customs);

            return SendPostCommand<Plan>(uri, jsonParams);
        }

        /// <summary>Updates one or more existing test runs in a plan (partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="planId">The ID of the test plan.</param>
        /// <param name="entryId">The ID of the test plan entry (note: not the test run ID).</param>
        /// <param name="name">The name of the test run(s).</param>
        /// <param name="assignedToId">The ID of the user the test run(s) should be assigned to.</param>
        /// <param name="caseIds">An array of case IDs for the custom case selection.</param>
        /// <param name="customs">Custom fields are also included in the response and use their system name prefixed with 'custom_' as their field identifier.</param>
        /// <returns>If successful, this method returns the updated test plan entry including test runs.</returns>
        public RequestResult<PlanEntry> UpdatePlanEntry(ulong planId, string entryId, string name = null, ulong? assignedToId = null, List<ulong> caseIds = null, JObject customs = null)
        {
            var uri = _CreateUri_(CommandType.Update, CommandAction.PlanEntry, planId, null, null, entryId);

            var planEntry = new PlanEntry
            {
                AssignedToId = assignedToId,
                Name = name,
                CaseIds = caseIds
            };

            var jsonParams = JsonUtility.Merge(planEntry.GetJson(), customs);

            return SendPostCommand<PlanEntry>(uri, jsonParams);
        }

        /// <summary>Updates an existing project (admin status required; partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <param name="projectName">The name of the project (required).</param>
        /// <param name="announcement">The description of the project.</param>
        /// <param name="showAnnouncement">True if the announcement should be displayed on the project's overview page and false otherwise.</param>
        /// <param name="isCompleted">Specifies whether a project is considered completed or not.</param>
        /// <returns>If successful, this method returns the updated project.</returns>
        public RequestResult<Project> UpdateProject(ulong projectId, string projectName, string announcement = null, bool? showAnnouncement = null, bool? isCompleted = null)
        {
            var uri = _CreateUri_(CommandType.Update, CommandAction.Project, projectId);

            var project = new Project
            {
                Name = projectName,
                Announcement = announcement,
                ShowAnnouncement = showAnnouncement,
                IsCompleted = isCompleted
            };

            return SendPostCommand<Project>(uri, project.GetJson());
        }

        /// <summary>Updates an existing test run (partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="runId">The ID of the test run.</param>
        /// <param name="name">The name of the test run.</param>
        /// <param name="description">The description of the test run.</param>
        /// <param name="milestoneId">The ID of the milestone to link to the test run.</param>
        /// <param name="caseIds">An array of case IDs for the custom case selection.</param>
        /// <param name="customs">Custom fields are also included in the response and use their system name prefixed with 'custom_' as their field identifier.</param>
        /// <returns>If successful, this method returns the updated test run.</returns>
        public RequestResult<Run> UpdateRun(ulong runId, string name = null, string description = null, ulong? milestoneId = null, HashSet<ulong> caseIds = null, JObject customs = null)
        {
            var includeAll = true;
            var existingRun = GetRun(runId).Payload;

            // validates whether we are in include all or custom case selection mode
            if (null != existingRun?.ProjectId && existingRun.SuiteId.HasValue && null != caseIds)
            {
                var atLeastOneCaseFoundInSuite = _CasesFoundInSuite(existingRun.ProjectId.Value, existingRun.SuiteId.Value, caseIds);

                if (atLeastOneCaseFoundInSuite)
                {
                    includeAll = false;
                }

                else
                {
                    return new RequestResult<Run>(HttpStatusCode.BadRequest, thrownException: new Exception("Case IDs not found in the Suite"));
                }
            }

            var uri = _CreateUri_(CommandType.Update, CommandAction.Run, runId);

            var newRun = new Run
            {
                Name = name,
                Description = description,
                MilestoneId = milestoneId,
                IncludeAll = includeAll,
                CaseIds = caseIds
            };

            var jsonParams = JsonUtility.Merge(newRun.GetJson(), customs);

            return SendPostCommand<Run>(uri, jsonParams);
        }

        /// <summary>Updates an existing section (partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="sectionId">The ID of the section.</param>
        /// <param name="name">The name of the section.</param>
        /// <param name="description">The description of the section (added with TestRail 4.0).</param>
        /// <param name="customs">Custom fields are also included in the response and use their system name prefixed with 'custom_' as their field identifier.</param>
        /// <returns>If successful, this method returns the updated section.</returns>
        public RequestResult<Section> UpdateSection(ulong sectionId, string name, string description = null, JObject customs = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new RequestResult<Section>(HttpStatusCode.BadRequest, thrownException: new ArgumentNullException(nameof(name)));
            }

            var uri = _CreateUri_(CommandType.Update, CommandAction.Section, sectionId);

            var section = new Section
            {
                Id = sectionId,
                Name = name
            };

            var jsonParams = JsonUtility.Merge(section.GetJson(), customs);

            return SendPostCommand<Section>(uri, jsonParams);
        }

        /// <summary>Updates an existing test suite (partial updates are supported, i.e. you can submit and update specific fields only).</summary>
        /// <param name="suiteId">The ID of the test suite.</param>
        /// <param name="name">The name of the test suite (required).</param>
        /// <param name="description">The description of the test suite.</param>
        /// <param name="customs">Custom fields are also included in the response and use their system name prefixed with 'custom_' as their field identifier.</param>
        /// <returns>If successful, this method returns the updated test suite.</returns>
        public RequestResult<Suite> UpdateSuite(ulong suiteId, string name = null, string description = null, JObject customs = null)
        {
            var uri = _CreateUri_(CommandType.Update, CommandAction.Suite, suiteId);

            var s = new Suite
            {
                Name = name,
                Description = description
            };

            var jsonParams = JsonUtility.Merge(s.GetJson(), customs);

            return SendPostCommand<Suite>(uri, jsonParams);
        }
        #endregion Update Commands

        #region Close Commands
        /// <summary>Closes an existing test plan and archives its test runs and results. Please note: Closing a test plan cannot be undone.</summary>
        /// <param name="planId">The ID of the test plan.</param>
        /// <returns>If successful, this method returns the closed test plan.</returns>
        public RequestResult<Plan> ClosePlan(ulong planId)
        {
            var uri = _CreateUri_(CommandType.Close, CommandAction.Plan, planId);
            var result = SendPostCommand<Plan>(uri);

            if (result.StatusCode != HttpStatusCode.OK)
            {
                OnOperationFailed(this, $"Could not close plan: {result.Payload.Id}");
            }

            return result;
        }

        /// <summary>Closes an existing test run and archives its tests and results. Please note: Closing a test run cannot be undone.</summary>
        /// <param name="runId">The ID of the test run.</param>
        /// <returns>If successful, this method returns the closed test run.</returns>
        public RequestResult<Run> CloseRun(ulong runId)
        {
            var uri = _CreateUri_(CommandType.Close, CommandAction.Run, runId);
            var result = SendPostCommand<Run>(uri);

            if (result.StatusCode != HttpStatusCode.OK)
            {
                OnOperationFailed(this, $"Could not close run : {result.Payload.Id}");
            }

            return result;
        }
        #endregion Close Commands

        #region Delete Commands
        /// <summary>Delete a milestone</summary>
        /// <param name="milestoneId">id of the milestone</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeleteMilestone(ulong milestoneId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.Milestone, milestoneId);

            return _SendCommand(uri);
        }

        /// <summary>Delete a case</summary>
        /// <param name="caseId">id of the case to delete</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeleteCase(ulong caseId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.Case, caseId);

            return _SendCommand(uri);
        }

        /// <summary>Delete a plan</summary>
        /// <param name="planId">id of the plan to delete</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeletePlan(ulong planId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.Plan, planId);

            return _SendCommand(uri);
        }

        /// <summary>Delete a specific plan entry for a plan id</summary>
        /// <param name="planId">id of the plan</param>
        /// <param name="entryId">string representation of the GUID for the entryID</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeletePlanEntry(ulong planId, string entryId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.PlanEntry, planId, null, null, entryId);

            return _SendCommand(uri);
        }

        /// <summary>Delete the Project</summary>
        /// <param name="projectId">id of the project to delete</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeleteProject(ulong projectId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.Project, projectId);

            return _SendCommand(uri);
        }

        /// <summary>Delete the section</summary>
        /// <param name="sectionId">id of the section to delete</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeleteSection(ulong sectionId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.Section, sectionId);

            return _SendCommand(uri);
        }

        /// <summary>Delete the suite</summary>
        /// <param name="suiteId">id of the suite to delete</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeleteSuite(ulong suiteId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.Suite, suiteId);

            return _SendCommand(uri);
        }

        /// <summary>Delete the run</summary>
        /// <param name="runId">id of the run to delete</param>
        /// <returns>result of the deletion</returns>
        public CommandResult<ulong> DeleteRun(ulong runId)
        {
            var uri = _CreateUri_(CommandType.Delete, CommandAction.Run, runId);
            return _SendCommand(uri);
        }
        #endregion Delete Commands

        #region Get Commands
        /// <summary>gets a test</summary>
        /// <param name="testId">id of the test</param>
        /// <returns>information about the test</returns>
        public RequestResult<Test> GetTest(ulong testId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Test, testId);

            return SendGetCommand<Test>(uri);
        }

        /// <summary>gets tests associated with a run</summary>
        /// <param name="runId">id of the run</param>
        /// <returns>tests associated with the run</returns>
        public RequestResult<IList<Test>> GetTests(ulong runId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Tests, runId);

            return SendGetCommand<IList<Test>>(uri);
        }

        /// <summary>gets a case</summary>
        /// <param name="caseId">id of the case</param>
        /// <returns>information about the case</returns>
        public RequestResult<Case> GetCase(ulong caseId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Case, caseId);

            return SendGetCommand<Case>(uri);
        }

        /// <summary>gets cases associated with a suite</summary>
        /// <param name="projectId">id of the project</param>
        /// <param name="suiteId">id of the suite</param>
        /// <param name="sectionId">(optional) id of the section</param>
        /// <returns>cases associated with the suite</returns>
        public RequestResult<IList<Case>> GetCases(ulong projectId, ulong suiteId, ulong? sectionId = null)
        {
            var optionalSectionId = sectionId.HasValue ? $"&section_id={sectionId.Value}" : string.Empty;
            var options = $"&suite_id={suiteId}{optionalSectionId}";
            var uri = _CreateUri_(CommandType.Get, CommandAction.Cases, projectId, null, options);

            return SendGetCommand<IList<Case>>(uri);
        }

        /// <summary>returns a list of available test case custom fields</summary>
        /// <returns>a list of custom field definitions</returns>
        public RequestResult<IList<CaseField>> GetCaseFields()
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.CaseFields);

            return SendGetCommand<IList<CaseField>>(uri);
        }

        /// <summary>returns a list of available case types</summary>
        /// <returns>a list of test case types, each has a unique ID and a name.</returns>
        public RequestResult<IList<CaseType>> GetCaseTypes()
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.CaseTypes);

            return SendGetCommand<IList<CaseType>>(uri);
        }

        /// <summary>gets a suite</summary>
        /// <param name="suiteId">id of the suite</param>
        /// <returns>information about the suite</returns>
        public RequestResult<Suite> GetSuite(ulong suiteId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Suite, suiteId);

            return SendGetCommand<Suite>(uri);
        }

        /// <summary>gets suites associated with a project</summary>
        /// <param name="projectId">id of the project</param>
        /// <returns>suites associated with the project</returns>
        public RequestResult<IList<Suite>> GetSuites(ulong projectId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Suites, projectId);

            return SendGetCommand<IList<Suite>>(uri);
        }

        /// <summary>gets a section</summary>
        /// <param name="sectionId">id of the section</param>
        /// <returns>information about the section</returns>
        public RequestResult<Section> GetSection(ulong sectionId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Section, sectionId);

            return SendGetCommand<Section>(uri);
        }

        /// <summary>gets sections associated with a suite</summary>
        /// <param name="projectId">id of the project</param>
        /// <param name="suiteId">id of the suite</param>
        /// <returns>sections associated with the suite</returns>
        public RequestResult<IList<Section>> GetSections(ulong projectId, ulong suiteId)
        {
            var options = $"&suite_id={suiteId}";
            var uri = _CreateUri_(CommandType.Get, CommandAction.Sections, projectId, null, options);

            return SendGetCommand<IList<Section>>(uri);
        }

        /// <summary>gets a run</summary>
        /// <param name="runId">id of the run</param>
        /// <returns>information about the run</returns>
        public RequestResult<Run> GetRun(ulong runId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Run, runId);

            return SendGetCommand<Run>(uri);
        }

        /// <summary>gets runs associated with a project</summary>
        /// <param name="projectId">id of the project</param>
        /// <returns>runs associated with the project</returns>
        public RequestResult<IList<Run>> GetRuns(ulong projectId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Runs, projectId);

            return SendGetCommand<IList<Run>>(uri);
        }

        /// <summary>gets a plan</summary>
        /// <param name="planId">id of the plan</param>
        /// <returns>information about the plan</returns>
        public RequestResult<Plan> GetPlan(ulong planId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Plan, planId);

            return SendGetCommand<Plan>(uri);
        }

        /// <summary>gets plans associated with a project</summary>
        /// <param name="projectId">id of the project</param>
        /// <returns>plans associated with the project</returns>
        public RequestResult<IList<Plan>> GetPlans(ulong projectId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Plans, projectId);

            return SendGetCommand<IList<Plan>>(uri);
        }

        /// <summary>gets a milestone</summary>
        /// <param name="milestoneId">id of the milestone</param>
        /// <returns>information about the milestone</returns>
        public RequestResult<Milestone> GetMilestone(ulong milestoneId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Milestone, milestoneId);

            return SendGetCommand<Milestone>(uri);
        }

        /// <summary>gets milestones associated with a project</summary>
        /// <param name="projectId">id of the project</param>
        /// <returns>milestone associated with project</returns>
        public RequestResult<IList<Milestone>> GetMilestones(ulong projectId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Milestones, projectId);

            return SendGetCommand<IList<Milestone>>(uri);
        }

        /// <summary>gets a project</summary>
        /// <param name="projectId">id of the project</param>
        /// <returns>information about the project</returns>
        public RequestResult<Project> GetProject(ulong projectId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Project, projectId);

            return SendGetCommand<Project>(uri);
        }

        /// <summary>gets all projects contained in the testrail instance</summary>
        /// <returns>list containing all the projects</returns>
        public RequestResult<IList<Project>> GetProjects()
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Projects);

            return SendGetCommand<IList<Project>>(uri);
        }

        /// <summary>Get User for user id</summary>
        /// <param name="userId">user id to search for</param>
        /// <returns>a User object</returns>
        public RequestResult<User> GetUser(ulong userId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.User, userId);

            return SendGetCommand<User>(uri);
        }

        /// <summary>Find a user by their email address</summary>
        /// <param name="email">email address of the user</param>
        /// <returns>user if found</returns>
        public RequestResult<User> GetUserByEmail(string email)
        {
            // validate the email string
            if (string.IsNullOrWhiteSpace(email))
            {
                return new RequestResult<User>(HttpStatusCode.BadRequest,
                    thrownException: new ArgumentException($"You must provide a valid string that is not null or white space for: {nameof(email)}"));
            }

            var optionalParam = $"&email={email}";
            var uri = _CreateUri_(CommandType.Get, CommandAction.UserByEmail, null, null, optionalParam);

            return SendGetCommand<User>(uri);
        }

        /// <summary>Get a list of users in the testrail instance</summary>
        /// <returns>List of users</returns>
        public RequestResult<IList<User>> GetUsers()
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Users);

            return SendGetCommand<IList<User>>(uri);
        }

        /// <summary>Returns a list of test results for a test</summary>
        /// <param name="testId">id of the test</param>
        /// <param name="limit">(optional) maximum amount of test results to return, latest first</param>
        /// <returns>list containing the results for the given test</returns>
        public RequestResult<IList<Result>> GetResults(ulong testId, ulong? limit = null)
        {
            var optional = (limit.HasValue) ? $"&limit={limit.Value}" : string.Empty;
            var uri = _CreateUri_(CommandType.Get, CommandAction.Results, testId, null, optional);

            return SendGetCommand<IList<Result>>(uri);
        }

        /// <summary>Return the list of test results for a test run and the case combination</summary>
        /// <param name="runId">id of the test run</param>
        /// <param name="caseId">id of the test case</param>
        /// <param name="limit">(optional) maximum amount of test results to return, latest first</param>
        /// <returns>list of test results for a case</returns>
        public RequestResult<IList<Result>> GetResultsForCase(ulong runId, ulong caseId, ulong? limit = null)
        {
            var optional = limit.HasValue ? $"&limit={limit.Value}" : string.Empty;
            var uri = _CreateUri_(CommandType.Get, CommandAction.ResultsForCase, runId, caseId, optional);

            return SendGetCommand<IList<Result>>(uri);
        }

        /// <summary>Return the list of test results for a test run</summary>
        /// <param name="runId">id of the rest run</param>
        /// <param name="limit">(optional) maximum amount of test results to return, latest first</param>
        /// <returns>list of test results for a test run</returns>
        public RequestResult<IList<Result>> GetResultsForRun(ulong runId, ulong? limit = null)
        {
            var optional = limit.HasValue ? $"&limit={limit.Value}" : string.Empty;
            var uri = _CreateUri_(CommandType.Get, CommandAction.ResultsForRun, runId, null, optional);

            return SendGetCommand<IList<Result>>(uri);
        }

        /// <summary>Returns the list of statuses available to test rail</summary>
        /// <returns>list of possible statuses</returns>
        public RequestResult<IList<Status>> GetStatuses()
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Statuses);

            return SendGetCommand<IList<Status>>(uri);
        }

        /// <summary>Get a list of all available priorities</summary>
        /// <returns>list of priorities</returns>
        public RequestResult<IList<Priority>> GetPriorities()
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Priorities);

            return SendGetCommand<IList<Priority>>(uri);
        }

        /// <summary>Returns a list of Config Groups available in a Project</summary>
        /// <param name="projectId">ID of the Project to return the Config Groups for</param>
        /// <returns>list of ConfigurationGroup</returns>
        public RequestResult<IList<ConfigurationGroup>> GetConfigurationGroups(ulong projectId)
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Configs, projectId);

            return SendGetCommand<IList<ConfigurationGroup>>(uri);
        }
        #endregion Get Commands
        #endregion Public Methods

        #region Protected Methods
        /// <summary>executes a get request for an item</summary>
        /// <typeparam name="T">the type of item</typeparam>
        /// <param name="actionName">the name of item's node</param>
        /// <param name="uri">the uri for the request</param>
        /// <param name="parse">a method which parse json into the item</param>
        /// <returns>object of the supplied type containing information about the item</returns>
        protected T _GetItem_<T>(CommandAction actionName, string uri, Func<JObject, T> parse) where T : BaseTestRailType, new()
        {
            var result = _CallEndpoint(uri, RequestType.Get);

            if (!result.WasSuccessful)
            {
                OnOperationFailed(this, $"Could not get {actionName}: {result.Value}");

                return default(T);
            }

            var json = JObject.Parse(result.Value);

            return parse(json);
        }

        /// <summary>executes a get request for an item</summary>
        /// <typeparam name="T">the type of the item</typeparam>
        /// <param name="actionName">the name of the item's node</param>
        /// <param name="uri">the uri for the request</param>
        /// <param name="parse">a method which parses the json into the item</param>
        /// <returns>list of objects of the supplied type corresponding th supplied filters</returns>
        protected List<T> _GetItems_<T>(CommandAction actionName, string uri, Func<JObject, T> parse) where T : BaseTestRailType, new()
        {
            var items = new List<T>();
            var result = _CallEndpoint(uri, RequestType.Get);

            if (!result.WasSuccessful)
            {
                OnOperationFailed(this, $"Could not get {actionName}s: {result.Value}");
            }

            else
            {
                var jarray = JArray.Parse(result.Value);

                if (null != jarray)
                {
                    items = JsonUtility.ConvertJArrayToList(jarray, parse);
                }
            }

            return items;
        }

        /// <summary>Creates a URI with the parameters given in the format</summary>
        /// <param name="commandType">the type of action the server is going to take (i.e. get, add, update, close)</param>
        /// <param name="actionName">the type of command the server is going to take (i.e. run, case, plan, etc)</param>
        /// <param name="id1">(optional)first id to include in the uri</param>
        /// <param name="id2">(optional)second id to include in the uri</param>
        /// <param name="options">(optional)additional options to include in the uri</param>
        /// <param name="id2Str">(optional)additional parameters to append to the uri</param>
        /// <returns>the newly created uri</returns>
        protected static string _CreateUri_(CommandType commandType, CommandAction actionName, ulong? id1 = null,
            ulong? id2 = null, string options = null, string id2Str = null)
        {
            var commandString = commandType.GetStringValue();
            var actionString = actionName.GetStringValue();

            var uri = $"?/api/v2/{commandString}_{actionString}{(id1.HasValue ? "/" + id1.Value : string.Empty)}{(id2.HasValue ? "/" + id2.Value : !string.IsNullOrWhiteSpace(id2Str) ? "/" + id2Str : string.Empty)}{(!string.IsNullOrWhiteSpace(options) ? options : string.Empty)}";

            return uri;
        }
        #endregion Protected Methods

        #region Private Methods
        /// <summary>Constructs the request and sends it.</summary>
        /// <param name="uri">The uri of the endpoint.</param>
        /// <param name="type">The type of request to build: GEt, POST, etc.</param>
        /// <param name="json">Parameters to send formatted as a single JSON object.</param>
        /// <returns>Result of the call.</returns>
        private CommandResult _CallEndpoint(string uri, RequestType type, JObject json = null)
        {
            uri = Url + uri;
            OnHttpRequestSent(this, new HttpRequestSentEventArgs(type.GetStringValue(), new Uri(uri)));

            CommandResult commandResult;
            string postContent = null;

            if (null != json)
            {
                postContent = json.ToString();
            }

            try
            {
                // Build request
                var request = new TestRailRequest(uri, type.GetStringValue());

                request.AddHeaders(new Dictionary<string, string> { { "Authorization", AuthInfo } });
                request.Accepts("application/json");
                request.ContentType("application/json");

                // Add body
                if (!string.IsNullOrWhiteSpace(postContent))
                {
                    request.AddBody(postContent);
                }

                // Send request
                commandResult = request.Execute();
            }

            catch (Exception e)
            {
                commandResult = new CommandResult(false, e.ToString());
            }

            if (!commandResult.WasSuccessful)
            {
                OnOperationFailed(this, $"HTTP RESPONSE: {commandResult.Value}");
            }

            else
            {
                OnHttpResponseReceived(this, commandResult.Value);
            }

            return commandResult;
        }

        private RequestResult<T> SendPostCommand<T>(string uri, JObject jsonParams = null)
        {
            try
            {
                return _SendCommand<T>(uri, RequestType.Post, jsonParams);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private RequestResult<T> SendGetCommand<T>(string uri)
        {
            return _SendCommand<T>(uri, RequestType.Get);
        }

        private RequestResult<T> _SendCommand<T>(string uri, RequestType type, JObject jsonParams = null)
        {
            try
            {
                var result = _CallEndpoint(uri, RequestType.Post, jsonParams);

                return new RequestResult<T>(HttpStatusCode.OK, result.Value);
            }

            // If there is an error, will try to create a new result object
            // with the corresponding response code
            catch (Exception thrownException)
            {
                var message = thrownException.Message;

                // Return a response object for the most popular errors
                if (message.Contains("400"))
                    return new RequestResult<T>(HttpStatusCode.BadRequest, thrownException: thrownException);

                if (message.Contains("401"))
                    return new RequestResult<T>(HttpStatusCode.Unauthorized, thrownException: thrownException);

                if (message.Contains("403"))
                    return new RequestResult<T>(HttpStatusCode.Forbidden, thrownException: thrownException);

                if (message.Contains("404"))
                    return new RequestResult<T>(HttpStatusCode.NotFound, thrownException: thrownException);

                if (message.Contains("500"))
                    return new RequestResult<T>(HttpStatusCode.InternalServerError, thrownException: thrownException);

                if (message.Contains("502"))
                    return new RequestResult<T>(HttpStatusCode.BadGateway, thrownException: thrownException);

                if (message.Contains("503"))
                    return new RequestResult<T>(HttpStatusCode.ServiceUnavailable, thrownException: thrownException);

                if (message.Contains("504"))
                    return new RequestResult<T>(HttpStatusCode.GatewayTimeout, thrownException: thrownException);

                throw;
            }
        }

        /// <summary>Send a command to the server</summary>
        /// <param name="uri">uri to send</param>
        /// <param name="jsonParams">parameters to send formatted as a single json object</param>
        /// <returns>object containing if the command: was successful, the result value, and any exception that may have been thrown by the server</returns>
        private CommandResult<ulong> _SendCommand(string uri, JObject jsonParams = null)
        {
            Exception exception = null;
            ulong resultValue = 0;
            var wasSuccessful = false;

            try
            {
                var result = _CallEndpoint(uri, RequestType.Post, jsonParams);

                wasSuccessful = result.WasSuccessful;

                if (wasSuccessful)
                {
                    if (!string.IsNullOrWhiteSpace(result.Value))
                    {
                        var json = JObject.Parse(result.Value);
                        var token = json["id"];

                        try
                        {
                            if (null == token)
                            {
                                // do nothing
                            }

                            else if (JTokenType.String == token.Type) // for plan entry 
                            {
                                resultValue = (ulong)json["runs"][0]["id"];
                            }

                            else if (JTokenType.Integer == token.Type)
                            {
                                resultValue = (ulong)json["id"];
                            }
                        }

                        catch
                        {
                            // do nothing since result value is already 0 
                        }
                    }
                }

                else
                {
                    exception = new Exception(result.Value);
                }
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }

            return new CommandResult<ulong>(wasSuccessful, resultValue, exception);
        }

        /// <summary>Determines if at least one of the case ids given is contained in the project and suite</summary>
        /// <param name="projectId">id of the project</param>
        /// <param name="suiteId">id of the suite</param>
        /// <param name="caseIds">collection of case ids to check</param>
        /// <returns>true if at least one case exists in the project and suite id combination, otherwise false</returns>
        private bool _CasesFoundInSuite(ulong projectId, ulong suiteId, ICollection<ulong> caseIds)
        {
            var validCases = GetCases(projectId, suiteId).Payload;

            return validCases.Any(tmpCase => tmpCase.Id.HasValue && caseIds.Contains(tmpCase.Id.Value));
        }

        /// <summary>Create a priority dictionary</summary>
        /// <returns>dictionary of priority ID (from test rail) to priority levels(where Higher value means higher priority)</returns>
        private Dictionary<ulong, int> _CreatePrioritiesDict()
        {
            var tmpDict = new Dictionary<ulong, int>();
            var priorityList = GetPriorities().Payload;

            foreach (var priority in priorityList.Where(priority => null != priority))
            {
                tmpDict[priority.Id] = priority.PriorityLevel;
            }

            return tmpDict;
        }

        private List<Project> InternalGetProjects()
        {
            var uri = _CreateUri_(CommandType.Get, CommandAction.Projects);

            return _GetItems_(CommandAction.Projects, uri, Project.Parse);
        }
        #endregion Private Methods
    }
}
