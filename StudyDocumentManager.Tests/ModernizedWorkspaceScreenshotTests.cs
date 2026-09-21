using System;
using System.Collections.Generic;
using System.IO;
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
    private static string GetScreenshotDirectory()
    {
        var customDir = Environment.GetEnvironmentVariable("SDM_SCREENSHOT_DIR");
        return !string.IsNullOrWhiteSpace(customDir)
            ? customDir
            : Path.Combine(Path.GetTempPath(), "sdm_screenshots");
    }

    [AvaloniaFact]
    public void Capture_ModernizedWorkspaces()
    {
        var outputDir = GetScreenshotDirectory();
        Directory.CreateDirectory(outputDir);

        int? createdDocId = null;
        int? createdAssignmentId = null;
        var createdCourseIds = new List<int>();
        var createdSemesterIds = new List<int>();

        var docRepo = App.Services!.GetRequiredService<IDocumentRepository>();
        var officeRepo = App.Services!.GetRequiredService<IOfficeMetadataRepository>();
        var assignRepo = App.Services!.GetRequiredService<IAssignmentRepository>();

        try
        {
            // 1. Office Workspace
            if (docRepo.GetAll().Count == 0)
            {
                var doc = new StudyDocument
                {
                    Name = "契約書_ドラフト_v2.pdf",
                    FilePath = @"C:\Sample\契約書_ドラフト_v2.pdf",
                    Subject = "法務",
                    Type = "PDF",
                    Status = DocumentStatus.InProgress,
                    CreatedAt = DateTime.UtcNow
                };
                docRepo.Add(doc);
                var allDocs = docRepo.GetAll();
                if (allDocs.Count > 0)
                {
                    createdDocId = allDocs[0].Id;
                    officeRepo.Save(new OfficeDocumentMetadata
                    {
                        DocumentId = createdDocId.Value,
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

            var officeModel = App.Services!.GetRequiredService<OfficeWorkspaceModel>();
            officeModel.Refresh();
            if (officeModel.FilteredRows.Count > 0)
            {
                officeModel.SelectedRow = officeModel.FilteredRows[0];
            }

            var officeWindow = new Window
            {
                Content = new OfficeWorkspace { DataContext = officeModel },
                Width = 1280,
                Height = 800
            };
            officeWindow.Show();
            Dispatcher.UIThread.RunJobs();
            var officeFrame = officeWindow.GetLastRenderedFrame();
            Assert.NotNull(officeFrame);
            var officeOutPath = Path.Combine(outputDir, "03_OfficeWorkspace.png");
            officeFrame.Save(officeOutPath);
            officeWindow.Close();
            Dispatcher.UIThread.RunJobs();

            // 2. Student Workspace
            if (assignRepo.GetCourses().Count == 0)
            {
                createdCourseIds.Add(assignRepo.AddCourse(new Course { Name = "コンピュータサイエンス基礎" }));
                createdCourseIds.Add(assignRepo.AddCourse(new Course { Name = "データ構造とアルゴリズム" }));
            }
            if (assignRepo.GetSemesters().Count == 0)
            {
                createdSemesterIds.Add(assignRepo.AddSemester(new Semester { Name = "2026年 前期", IsActive = true }));
            }
            if (assignRepo.GetAssignments().Count == 0)
            {
                var sems = assignRepo.GetSemesters();
                var courses = assignRepo.GetCourses();
                createdAssignmentId = assignRepo.AddAssignment(new Assignment
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

            var studentModel = App.Services!.GetRequiredService<StudentWorkspaceModel>();
            studentModel.RefreshCommand.Execute(null);
            if (studentModel.Assignments.Count > 0)
            {
                studentModel.SelectedAssignment = studentModel.Assignments[0];
                studentModel.EditAssignmentCommand.Execute(null);
            }

            var studentWindow = new Window
            {
                Content = new StudentWorkspace { DataContext = studentModel },
                Width = 1280,
                Height = 800
            };
            studentWindow.Show();
            Dispatcher.UIThread.RunJobs();
            var studentFrame = studentWindow.GetLastRenderedFrame();
            Assert.NotNull(studentFrame);
            var studentOutPath = Path.Combine(outputDir, "04_StudentWorkspace.png");
            studentFrame.Save(studentOutPath);
            studentWindow.Close();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            // テスト用に追加したエンティティのクリーンアップ
            if (createdDocId.HasValue)
            {
                officeRepo.DeleteByDocumentId(createdDocId.Value);
                docRepo.Delete(createdDocId.Value);
            }

            if (createdAssignmentId.HasValue)
            {
                assignRepo.DeleteAssignment(createdAssignmentId.Value);
            }

            foreach (var courseId in createdCourseIds)
            {
                assignRepo.DeleteCourse(courseId);
            }

            foreach (var semesterId in createdSemesterIds)
            {
                assignRepo.DeleteSemester(semesterId);
            }
        }
    }
}
