# 📝 Content Extraction Guide - HTML to Word/Docs

## Purpose
This guide helps you extract content from your existing `index.html` web manual and transfer it to your Word/Google Docs document.

---

## 🔍 Content Mapping

### From HTML to Document

Your HTML file has the following structure that maps to your PDF sections:

| HTML Section ID | PDF Section Name | Line Range (approx) |
|----------------|------------------|---------------------|
| `#getting-started` | 🚀 Getting Started | Lines 104-150 |
| `#login` | 🔐 Login & Registration | Lines 153-243 |
| `#dashboard` | 📊 Dashboard | Lines 247-296 |
| `#master-data` | 🏢 Master Data Management | Lines 299-403 |
| `#items` | 📦 Item Management | Lines 406-507 |
| `#inventory` | 📊 Inventory Management | Lines 510-559 |
| `#requests` | 📝 Request Management | Lines 562-587 |
| `#sets` | 📋 Sets & Invoices | Lines 590-703 |
| `#view-receipts` | 🧾 View Receipts | Lines 706-765 |
| `#renewals` | 🔄 Renewal Management | Lines 768-786 |
| `#warranty` | 🛡️ Warranty Management | Lines 789-833 |
| `#archive` | 🗄️ Archive Management | Lines 836-854 |
| `#mobile-updates` | 📱 Mobile Updates | Lines 857-892 |
| `#audit-trail` | 👣 Audit Trail | Lines 895-931 |
| `#reports` | 📈 Reports | Lines 934-951 |
| `#notifications` | 🔔 Notifications | Lines 954-978 |
| `#troubleshooting` | 🔧 Troubleshooting | Lines 981-1000 |

---

## 📋 Extraction Methods

### Method 1: View in Browser & Copy (Recommended)

1. **Open `index.html` in browser**
   - Double-click the file
   - Or right-click → Open with → Chrome/Edge

2. **For each section:**
   - Click the section in the sidebar
   - Select the content (avoid selecting navigation)
   - Copy (Ctrl+C)
   - Paste into Word/Docs (Ctrl+V)
   - Clean up formatting as needed

**Pros:** 
- ✅ Preserves basic formatting
- ✅ Easy to see what you're copying
- ✅ No HTML knowledge needed

**Cons:**
- ❌ May include unwanted elements
- ❌ Formatting needs cleanup

---

### Method 2: Edit HTML Directly

1. **Open `index.html` in text editor** (VS Code, Notepad++)

2. **Find content between tags:**
   ```html
   <section id="getting-started">
       <!-- Content is here -->
   </section>
   ```

3. **Extract text content:**
   - Ignore HTML tags (`<div>`, `<p>`, `<ul>`, etc.)
   - Copy just the text content
   - Paste into Word/Docs
   - Apply formatting manually

**Pros:**
- ✅ Clean content extraction
- ✅ No unwanted formatting
- ✅ Full control

**Cons:**
- ❌ Requires HTML knowledge
- ❌ More time-consuming
- ❌ Need to format everything manually

---

### Method 3: Use Online HTML to Word Converter

1. **Visit converter site:**
   - https://www.zamzar.com/convert/html-to-docx/
   - https://cloudconvert.com/html-to-docx
   - Or search "HTML to Word converter"

2. **Upload `index.html`**

3. **Download converted DOCX**

4. **Clean up and reformat**

**Pros:**
- ✅ Quick conversion
- ✅ Preserves some structure

**Cons:**
- ❌ Formatting often messy
- ❌ Requires significant cleanup
- ❌ May not preserve all content

---

## 🎯 Recommended Workflow

### Step-by-Step Process

#### 1. Setup Your Document (30 min)
- [ ] Create new Word/Google Docs document
- [ ] Set up page layout (margins, size)
- [ ] Create cover page
- [ ] Set up heading styles (H1, H2, H3)
- [ ] Create callout box templates

#### 2. Extract Content Section by Section (3-4 hours)

**For each section:**

1. **Open browser with `index.html`**
2. **Navigate to section** (click sidebar link)
3. **Copy section heading** (e.g., "🚀 Getting Started")
4. **Paste into Word/Docs** and apply Heading 1 style
5. **Copy subsection content:**
   - Headings → Apply Heading 2 or 3
   - Paragraphs → Apply Body Text style
   - Lists → Use bullet or numbered lists
   - Tables → Recreate in Word/Docs
   - Tips/Warnings → Use callout boxes
6. **Insert related images** from `IMAGES/` folder
7. **Add captions** to images
8. **Move to next section**

#### 3. Insert Images (1-2 hours)

**Image Checklist:**
- [ ] LOGIN_PAGE.png → Login section
- [ ] REGISTER_PAGE.png → Registration section
- [ ] DASHBOARD_PAGE.png → Dashboard section
- [ ] ADD_COMPANY_PAGE.png → Company Management
- [ ] ADD_DEPARTMENT_PAGE.png → Department Management
- [ ] ADD_BRANCH_PAGE.png → Branch Management
- [ ] ADD_EMPLOYEE_PAGE.png → Employee Management
- [ ] ADD_VENDOR_PAGE.png → Vendor Management
- [ ] ADD_ITEM_PAGE.png → Item Management
- [ ] ADD_REQUEST_PAGE.png → Request Management
- [ ] SET_DETAILS.png → Sets & Invoices
- [ ] ADD_RECEIPT_PAGE.png → View Receipts
- [ ] renewal_details.png → Renewal Management
- [ ] VIEW_ARCHIVE.png → Archive Management
- [ ] VIEW_UPDATES.png → Mobile Updates
- [ ] VIEW_AUDIT_TRAIL.png → Audit Trail
- [ ] INVOICE_REPORT.png → Reports
- [ ] NOTIFICATION_CENTER.png → Notifications
- [ ] User_Change_password.png → Change Password (User)
- [ ] Dev_Change_password.png → Change Password (Developer)

**For each image:**
1. Insert → Picture → From file
2. Resize to appropriate width (4-6.5 inches)
3. Center align
4. Add caption below (Italic, 10pt)

#### 4. Format & Polish (1-2 hours)
- [ ] Apply consistent heading styles
- [ ] Format all tables consistently
- [ ] Create callout boxes for tips/warnings
- [ ] Add page breaks before major sections
- [ ] Check spacing and alignment
- [ ] Verify all images are visible

#### 5. Final Review (30 min)
- [ ] Generate/Update Table of Contents
- [ ] Check page numbers
- [ ] Spell check (F7)
- [ ] Review on print preview
- [ ] Get colleague feedback

#### 6. Export to PDF (10 min)
- [ ] Update TOC one final time
- [ ] File → Save As → PDF
- [ ] Enable bookmarks option
- [ ] Test PDF on different devices

---

## 🔄 HTML Element Translation

### Common HTML to Word/Docs Conversions

| HTML Element | Word/Docs Equivalent |
|-------------|---------------------|
| `<h2>` | Heading 1 style |
| `<h3>` | Heading 2 style |
| `<h4>` | Heading 3 style |
| `<p>` | Body Text style |
| `<ul><li>` | Bulleted list |
| `<ol><li>` | Numbered list |
| `<strong>` | Bold text |
| `<em>` | Italic text |
| `<code>` | Consolas font, gray background |
| `<table>` | Insert → Table |
| `<img>` | Insert → Picture |
| `<div class="tip">` | Callout box (blue) |
| `<div class="warning">` | Callout box (orange) |

---

## 💡 Content Extraction Tips

### 1. Work in Batches
Don't try to do everything at once:
- Day 1: Sections 1-5
- Day 2: Sections 6-10
- Day 3: Sections 11-17 + Polish

### 2. Use Find & Replace
After pasting content, use Find & Replace to:
- Remove extra spaces: Find `  ` (double space) → Replace ` ` (single space)
- Fix quotes: Find `"` → Replace `"` (smart quotes)
- Clean up bullets: Standardize bullet characters

### 3. Keep HTML Open as Reference
Have both documents open side-by-side:
- Browser with HTML (left)
- Word/Docs (right)
- Copy section by section

### 4. Don't Worry About Perfect Formatting Initially
First pass: Get all content in
Second pass: Format everything consistently
Third pass: Polish and refine

### 5. Save Frequently
- Save every 15-30 minutes
- Use version names: `YIMS_Manual_v1_draft.docx`
- Keep backups

---

## 🎨 Formatting Quick Reference

### After Pasting Content

1. **Select all pasted content**
2. **Clear formatting:** Ctrl+Space (Word) or Format → Clear formatting (Docs)
3. **Reapply styles:**
   - Main heading → Heading 1
   - Subsections → Heading 2
   - Sub-subsections → Heading 3
   - Body text → Normal/Body Text
4. **Format lists:**
   - Select list items
   - Click bullet or numbering button
5. **Format special elements:**
   - Tips → Create callout box
   - Code → Apply code style
   - Tables → Recreate using Insert Table

---

## 📊 Table Recreation Guide

### Example: Item Conditions Table

**HTML Version:**
```html
<table>
    <thead>
        <tr>
            <th>Condition</th>
            <th>Description</th>
            <th>Color Code</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>Perfect</td>
            <td>Fully functional, no issues</td>
            <td>Green</td>
        </tr>
        <tr>
            <td>Damaged</td>
            <td>Major issues, unusable</td>
            <td>Red</td>
        </tr>
    </tbody>
</table>
```

**Word/Docs Steps:**
1. Insert → Table → 3 columns × 3 rows
2. **Row 1 (Header):**
   - Type: Condition | Description | Color Code
   - Format: Bold, White text, Yakult Red background
3. **Row 2:**
   - Type: Perfect | Fully functional, no issues | Green
4. **Row 3:**
   - Type: Damaged | Major issues, unusable | Red
5. **Format table:**
   - Apply borders (1pt, light gray)
   - Alternate row shading (light gray for even rows)
   - Center align header text
   - Left align body text

---

## ✅ Quality Checklist

### Content Completeness
- [ ] All 17 sections included
- [ ] All subsections present
- [ ] No missing paragraphs
- [ ] All lists complete
- [ ] All tables recreated

### Formatting Consistency
- [ ] All headings use correct styles
- [ ] Consistent font throughout
- [ ] Uniform spacing
- [ ] Tables formatted identically
- [ ] Callout boxes styled consistently

### Images
- [ ] All 20+ images inserted
- [ ] Images properly sized
- [ ] Captions added to all images
- [ ] Images centered
- [ ] Good image quality

### Professional Polish
- [ ] No spelling errors
- [ ] No grammar issues
- [ ] Consistent terminology
- [ ] Page breaks appropriate
- [ ] Headers/footers present

---

## 🆘 Troubleshooting

### Problem: Formatting looks messy after pasting
**Solution:** Clear all formatting (Ctrl+Space) and reapply styles manually

### Problem: Images won't insert
**Solution:** Check image file path, ensure images are in accessible location

### Problem: Table of Contents not updating
**Solution:** Right-click TOC → Update Field → Update entire table

### Problem: Document file size too large
**Solution:** Compress images before inserting (use online image compressor)

### Problem: PDF export missing bookmarks
**Solution:** Ensure "Create bookmarks using Headings" is checked in export options

---

## 📞 Need Help?

If you get stuck:
1. **Check the PDF_Template_Guide.md** for formatting help
2. **Review HTML in browser** to see original content
3. **Use Word/Docs Help** (F1) for specific features
4. **Ask a colleague** to review your progress

---

**Happy content extraction! 📝✨**
