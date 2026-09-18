-- ============================================================
-- Migration: EmptyCartridge — add DisposalCompanyName column
-- Date:      2026-02-21
-- Reason:    Optional free-text field for recording the company
--            that received the cartridge when IT disposes of it
--            or sells it internally.  No FK, no vendor list,
--            no mandatory input — purely informational metadata.
-- ============================================================

ALTER TABLE [dbo].[EmptyCartridge]
    ADD [DisposalCompanyName] NVARCHAR(200) NULL;
GO
