using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class UpdateBadgeScreenshotTests
{
    private static readonly string[] ScreenshotDirs =
    [
        @"C:\Users\ADMIN\.gemini\antigravity-cli\brain\514e191a-c12e-423e-882c-21fcb8e64477\screenshots",
        @"C:\Users\ADMIN\.gemini\antigravity-cli\brain\52f12f8f-7bf7-4eb1-87ac-d5a0904b69e0\screenshots"
    ];

    [AvaloniaFact]
    public void Capture_UpdateBadgeScreenshots()
    {
        foreach (var dir in ScreenshotDirs)
        {
            Directory.CreateDirectory(dir);
        }

        var docRepo = App.Services!.GetRequiredService<IDocumentRepository>();
        var officeRepo = App.Services!.GetRequiredService<IOfficeMetadataRepository>();
        var assignRepo = App.Services!.GetRequiredService<IAssignmentRepository>();

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

        var mainModel = App.Services!.GetRequiredService<MainWindowModel>();
        var navService = App.Services!.GetRequiredService<NavigationService>();
        navService.SetMainModel(mainModel);

        // Simulate update available v4.2.0
        mainModel.HasUpdateAvailable = true;
        mainModel.UpdateVersionText = "v4.2.0";
        mainModel.LatestUpdateInfo = new UpdateInfo
        {
            HasUpdate = true,
            NewVersion = "v4.2.0"
        };

        // 1. Dashboard with Update Badge
        {
            navService.NavigateTo("dashboard");
            var window = new MainWindow
            {
                DataContext = mainModel,
                Width = 1280,
                Height = 800
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var frame = window.GetLastRenderedFrame();
            foreach (var dir in ScreenshotDirs)
            {
                frame?.Save(Path.Combine(dir, "00_MainWindow_UpdateBadge_Dashboard.png"));
            }
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        // 2. OfficeWorkspace with Update Badge & Global Back button
        {
            navService.NavigateTo("office-workspace");
            var officeModel = (OfficeWorkspaceModel)mainModel.CurrentView;
            officeModel.Refresh();
            if (officeModel.FilteredRows.Count > 0)
            {
                officeModel.SelectedRow = officeModel.FilteredRows[0];
            }
            var window = new MainWindow
            {
                DataContext = mainModel,
                Width = 1280,
                Height = 800
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var frame = window.GetLastRenderedFrame();
            foreach (var dir in ScreenshotDirs)
            {
                frame?.Save(Path.Combine(dir, "00_MainWindow_UpdateBadge_OfficeWorkspace.png"));
            }
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        // 3. StudentWorkspace with Update Badge & Global Back button
        {
            navService.NavigateTo("student-workspace");
            var studentModel = (StudentWorkspaceModel)mainModel.CurrentView;
            studentModel.RefreshCommand.Execute(null);
            if (studentModel.Assignments.Count > 0)
            {
                studentModel.SelectedAssignment = studentModel.Assignments[0];
            }
            var window = new MainWindow
            {
                DataContext = mainModel,
                Width = 1280,
                Height = 800
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var frame = window.GetLastRenderedFrame();
            foreach (var dir in ScreenshotDirs)
            {
                frame?.Save(Path.Combine(dir, "00_MainWindow_UpdateBadge_StudentWorkspace.png"));
            }
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
