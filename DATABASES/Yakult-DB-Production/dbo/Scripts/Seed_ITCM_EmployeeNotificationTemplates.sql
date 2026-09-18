/* ITCM employee assignment/escalation notification templates.
   Safe to run repeatedly; existing templates are preserved. */
IF OBJECT_ID(N'dbo.CallEmailTemplate', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.CallEmailTemplate WHERE TemplateType = N'Assignment')
    BEGIN
        INSERT dbo.CallEmailTemplate (TemplateType, Subject, Body, IsActive)
        VALUES
        (
            N'Assignment',
            N'[IT Call Monitoring] Ticket assigned to you - {{TicketCode}}',
            N'Hello,\n\nTicket {{TicketCode}} has been assigned to you in IT Call Monitoring.\n\nIssue: {{Issue}}\nPriority: {{Priority}}\nStatus: {{Status}}\nCompany: {{Company}}\nDepartment: {{Department}}\nBranch: {{Branch}}\nCaller: {{CallerName}}\nAssigned to: {{AssignedTo}}\n\nPlease open IT Call Monitoring to review and work on this ticket.\n\nIT Call Monitoring',
            1
        );
    END
    ELSE
    BEGIN
        UPDATE dbo.CallEmailTemplate
        SET IsActive = 1
        WHERE TemplateType = N'Assignment'
          AND IsActive = 0;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.CallEmailTemplate WHERE TemplateType = N'Escalation')
    BEGIN
        INSERT dbo.CallEmailTemplate (TemplateType, Subject, Body, IsActive)
        VALUES
        (
            N'Escalation',
            N'[IT Call Monitoring] Ticket escalated - {{TicketCode}}',
            N'Hello,\n\nTicket {{TicketCode}} has been escalated in IT Call Monitoring.\n\nIssue: {{Issue}}\nPriority: {{Priority}}\nPrevious status: {{OldStatus}}\nCurrent status: {{NewStatus}}\nCompany: {{Company}}\nDepartment: {{Department}}\nBranch: {{Branch}}\nCaller: {{CallerName}}\nCurrent assignee: {{AssignedTo}}\nReason: {{Note}}\n\nPlease review this ticket as soon as possible.\n\nIT Call Monitoring',
            1
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.CallEmailTemplate WHERE TemplateType = N'Reassignment')
    BEGIN
        INSERT dbo.CallEmailTemplate (TemplateType, Subject, Body, IsActive)
        VALUES
        (
            N'Reassignment',
            N'[IT Call Monitoring] Ticket reassigned to you - {{TicketCode}}',
            N'Hello,' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
            N'Ticket {{TicketCode}} has been reassigned to you in IT Call Monitoring.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
            N'Previous IT employee: {{PreviousAssignee}}' + CHAR(13) + CHAR(10) +
            N'Assigned to: {{AssignedTo}}' + CHAR(13) + CHAR(10) +
            N'Issue: {{Issue}}' + CHAR(13) + CHAR(10) +
            N'Priority: {{Priority}}' + CHAR(13) + CHAR(10) +
            N'Status: {{Status}}' + CHAR(13) + CHAR(10) +
            N'Company: {{Company}}' + CHAR(13) + CHAR(10) +
            N'Department: {{Department}}' + CHAR(13) + CHAR(10) +
            N'Branch: {{Branch}}' + CHAR(13) + CHAR(10) +
            N'Caller: {{CallerName}}' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
            N'Please open IT Call Monitoring to review and continue work on this ticket.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
            N'IT Call Monitoring',
            1
        );
    END
    ELSE
    BEGIN
        UPDATE dbo.CallEmailTemplate
        SET IsActive = 1
        WHERE TemplateType = N'Reassignment'
          AND IsActive = 0;
    END;
END;
GO
