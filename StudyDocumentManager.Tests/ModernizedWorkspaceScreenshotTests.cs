using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class ModernizedWorkspaceScreenshotTests
{
    private static readonly string[] ScreenshotDirs =
    [
        @"C:\Users\ADMIN\.gemini\antigravity-cli\brain\52f12f8f-7bf7-4eb1-87ac-d5a0904b69e0\screenshots",
        @"C:\Users\ADMIN\.gemini\antigravity-cli\brain\f4eaec18-0c78-44ee-93e6-52f0f00e920d\screenshots",
        @"C:\Users\ADMIN\.gemini\antigravity-cli\brain\bf017a99-cde4-41f6-81af-b868e39262af\screenshots"
    ];

    [AvaloniaFact]
    public void Capture_ModernizedWorkspaces()
    {
        foreach (var dir in ScreenshotDirs)
        {
            Directory.CreateDirectory(dir);
        }

        // 1. Office Workspace
        {
            var docRepo = App.Services!.GetRequiredService<IDocumentRepository>();
            var officeRepo = App.Services!.GetRequiredService<IOfficeMetadataRepository>();
            if (docRepo.GetAll().Count == 0)
            {
                docRepo.Add(new StudyDocument
                {
                    Name = "契約書_ドラフト_v2.pdf",
                    FilePath = @"C:\Sample\契約書_ドラフト_v2.pdf",
                    Subject = "法務",
                    Type = "PDF",
                    Status = DocumentStatus.InProgress,
                    CreatedAt = DateTime.UtcNow
                });
                var allDocs = docRepo.GetAll();
                if (allDocs.Count > 0)
                {
                    officeRepo.Save(new OfficeDocumentMetadata
                    {
                        DocumentId = allDocs[0].Id,
                        DocumentNumber = "DOC-2026-089",
                        OrganizationOrProject = "プロジェクトアルファ",
                        ContactName = "山田 太郎",
                        EffectiveDate = DateTime.Today,
                        ExpiryDate = DateTime.Today.AddDays(30),
                        ConfidentialityLevel = OfficeConfidentialityLevel.Internal,
                        ReminderEnabled = true,
                        ReminderDaysBefore = 7
                    });
                }
            }

            var model = App.Services!.GetRequiredService<OfficeWorkspaceModel>();
            model.Refresh();
            if (model.FilteredRows.Count > 0)
            {
                model.SelectedRow = model.FilteredRows[0];
            }

            var window = new Window
            {
                Content = new OfficeWorkspace { DataContext = model },
                Width = 1280,
                Height = 800
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var frame = window.GetLastRenderedFrame();
            foreach (var dir in ScreenshotDirs)
            {
                var outPath = Path.Combine(dir, "03_OfficeWorkspace.png");
                frame?.Save(outPath);
            }
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        // 2. Student Workspace
        {
            var assignRepo = App.Services!.GetRequiredService<IAssignmentRepository>();
            if (assignRepo.GetCourses().Count == 0)
            {
                assignRepo.AddCourse(new Course { Name = "コンピュータサイエンス基礎" });
                assignRepo.AddCourse(new Course { Name = "データ構造とアルゴリズム" });
            }
            if (assignRepo.GetSemesters().Count == 0)
            {
                assignRepo.AddSemester(new Semester { Name = "2026年 前期", IsActive = true });
            }
            if (assignRepo.GetAssignments().Count == 0)
            {
                var sems = assignRepo.GetSemesters();
                var courses = assignRepo.GetCourses();
                assignRepo.AddAssignment(new Assignment
                {
                    Title = "アルゴリズム第3回復習レポート",
                    CourseId = courses.Count > 0 ? courses[0].Id : null,
                    SemesterId = sems.Count > 0 ? sems[0].Id : 1,
                    OfficialDeadline = DateTime.Today.AddDays(7),
                    PersonalDeadline = DateTime.Today.AddDays(5),
                    Status = AssignmentStatuses.InProgress,
                    Priority = AssignmentPriorities.High,
                    Milestone = "ドラフト提出",
                    Notes = "第3章の計算量解析を重点的にまとめること。"
                });
            }

            var model = App.Services!.GetRequiredService<StudentWorkspaceModel>();
            model.RefreshCommand.Execute(null);
            if (model.Assignments.Count > 0)
            {
                model.SelectedAssignment = model.Assignments[0];
                model.EditAssignmentCommand.Execute(null);
            }

            var window = new Window
            {
                Content = new StudentWorkspace { DataContext = model },
                Width = 1280,
                Height = 800
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var frame = window.GetLastRenderedFrame();
            foreach (var dir in ScreenshotDirs)
            {
                var outPath = Path.Combine(dir, "04_StudentWorkspace.png");
                frame?.Save(outPath);
            }
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
