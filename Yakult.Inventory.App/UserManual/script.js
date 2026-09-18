// Yakult Inventory System - User Manual JavaScript

document.addEventListener('DOMContentLoaded', function () {
    // 1. Collapsible Section Logic
    const toggleSection = (header) => {
        const section = header.parentElement;
        section.classList.toggle('active');
    };

    // Attach click listeners to all section headers
    // Note: We'll do this dynamically if sections are created dynamically, 
    // but for now assuming static structure or re-attaching after search
    // Attach click listeners to all section headers
    document.querySelectorAll('.section-header').forEach(header => {
        header.addEventListener('click', () => toggleSection(header));
    });

    // 1.5 Sidebar Toggle Logic
    const sidebarToggle = document.getElementById('sidebar-toggle');
    if (sidebarToggle) {
        sidebarToggle.addEventListener('click', () => {
            document.body.classList.toggle('collapsed-sidebar');
        });
    }

    // 1.6 Global Expand/Collapse Logic
    const globalToggle = document.getElementById('global-toggle');
    if (globalToggle) {
        globalToggle.addEventListener('click', () => {
            const sections = document.querySelectorAll('.section');
            const isExpanded = globalToggle.textContent.includes('Collapse');

            sections.forEach(section => {
                if (isExpanded) {
                    section.classList.remove('active');
                } else {
                    section.classList.add('active');
                }
            });

            globalToggle.textContent = isExpanded ? 'Expand All' : 'Collapse All';
        });
    }

    // 1.7 Category Filter Logic
    const filterBtns = document.querySelectorAll('.filter-btn');
    filterBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            // Remove active class from all buttons
            filterBtns.forEach(b => b.classList.remove('active'));
            // Add active to clicked button
            btn.classList.add('active');

            const filterValue = btn.getAttribute('data-filter');
            const allSections = document.querySelectorAll('.section');

            allSections.forEach(section => {
                const category = section.getAttribute('data-category');

                if (filterValue === 'all' || category === filterValue) {
                    section.classList.remove('hidden');
                    // Optional: Auto-expand filtered sections for better visibility
                    // section.classList.add('active'); 
                } else {
                    section.classList.add('hidden');
                    section.classList.remove('active'); // Collapse hidden ones
                }
            });
        });
    });

    // 2. Real-Time Search Functionality
    const searchInput = document.getElementById('search-input');

    if (searchInput) {
        searchInput.addEventListener('input', function (e) {
            const searchTerm = e.target.value.toLowerCase();
            const sections = document.querySelectorAll('.section');

            // Function to clear highlights
            const clearHighlights = (element) => {
                const highlights = element.querySelectorAll('.highlight');
                highlights.forEach(hl => {
                    const parent = hl.parentNode;
                    parent.replaceChild(document.createTextNode(hl.textContent), hl);
                    parent.normalize();
                });
            };

            // Function to apply highlights safely to text nodes
            const applyHighlight = (element, term) => {
                if (!term) return;
                const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT, null, false);
                const nodes = [];
                let node;
                while (node = walker.nextNode()) nodes.push(node);

                nodes.forEach(textNode => {
                    const text = textNode.textContent;
                    const index = text.toLowerCase().indexOf(term);
                    if (index >= 0) {
                        const span = document.createElement('span');
                        span.innerHTML = text.replace(new RegExp(`(${term})`, 'gi'), '<span class="highlight">$1</span>');
                        textNode.parentNode.replaceChild(span, textNode);
                    }
                });
            };

            sections.forEach(section => {
                // Clear previous results
                clearHighlights(section);

                if (searchTerm.length < 2) {
                    section.classList.remove('hidden');
                    // Reset cards within section
                    section.querySelectorAll('.card').forEach(c => c.style.display = 'block');
                    return;
                }

                const content = section.innerText.toLowerCase();
                const cards = section.querySelectorAll('.card');
                let sectionHasMatch = false;

                cards.forEach(card => {
                    if (card.innerText.toLowerCase().includes(searchTerm)) {
                        card.style.display = 'block';
                        applyHighlight(card, searchTerm);
                        sectionHasMatch = true;
                    } else {
                        card.style.display = 'none';
                    }
                });

                if (sectionHasMatch) {
                    section.classList.remove('hidden');
                    section.classList.add('active');
                } else {
                    section.classList.add('hidden');
                    section.classList.remove('active');
                }
            });
        });
    }

    // 3. Lightbox Functionality
    const createLightbox = () => {
        const modal = document.createElement('div');
        modal.className = 'lightbox-modal';
        modal.innerHTML = `
            <span class="close-lightbox">&times;</span>
            <img class="lightbox-content" id="lightbox-img">
        `;
        document.body.appendChild(modal);

        const modalImg = modal.querySelector('#lightbox-img');
        const closeBtn = modal.querySelector('.close-lightbox');

        // Close on click
        closeBtn.onclick = () => {
            modal.classList.remove('show');
            setTimeout(() => modal.style.display = "none", 300);
        };

        modal.onclick = (e) => {
            if (e.target === modal) {
                modal.classList.remove('show');
                setTimeout(() => modal.style.display = "none", 300);
            }
        };

        return { modal, modalImg };
    };

    const { modal, modalImg } = createLightbox();

    // Attach click event to all images in content sections
    document.querySelectorAll('.section-content img').forEach(img => {
        img.addEventListener('click', function () {
            modal.style.display = "block";
            // slight delay to allow display:block to apply before opacity transition
            setTimeout(() => modal.classList.add('show'), 10);
            modalImg.src = this.src;
        });
    });

    // 4. Existing Utilities (Smooth Scroll, Copy Button, Back to Top)

    // Smooth scrolling for navigation links
    const navLinks = document.querySelectorAll('.nav-link');
    navLinks.forEach(link => {
        link.addEventListener('click', function (e) {
            e.preventDefault();
            navLinks.forEach(l => l.classList.remove('active'));
            this.classList.add('active');

            const targetId = this.getAttribute('href');
            const targetSection = document.querySelector(targetId);

            if (targetSection) {
                // Expand the target section
                targetSection.classList.add('active');

                targetSection.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });

    // Copy Button & Back to Top (Preserved from original)
    const codeBlocks = document.querySelectorAll('pre code');
    codeBlocks.forEach(block => {
        const button = document.createElement('button');
        button.className = 'copy-btn';
        button.textContent = 'Copy';
        button.style.cssText = `position: absolute; top: 5px; right: 5px; padding: 5px 10px; background: #3498db; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px;`;

        const pre = block.parentElement;
        pre.style.position = 'relative';
        pre.appendChild(button);

        button.addEventListener('click', function () {
            navigator.clipboard.writeText(block.textContent).then(() => {
                button.textContent = 'Copied!';
                setTimeout(() => button.textContent = 'Copy', 2000);
            });
        });
    });

    const backToTopBtn = document.createElement('button');
    backToTopBtn.innerHTML = '↑';
    backToTopBtn.className = 'back-to-top';
    backToTopBtn.style.cssText = `position: fixed; bottom: 30px; right: 30px; width: 50px; height: 50px; background: #e31e24; color: white; border: none; border-radius: 50%; font-size: 24px; cursor: pointer; display: none; box-shadow: 0 4px 12px rgba(0,0,0,0.2); transition: all 0.3s ease; z-index: 1000;`;
    document.body.appendChild(backToTopBtn);

    backToTopBtn.addEventListener('click', () => {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });

    window.addEventListener('scroll', function () {
        if (window.pageYOffset > 300) {
            backToTopBtn.style.display = 'block';
        } else {
            backToTopBtn.style.display = 'none';
        }
    });
    // Tooltips for badges
    const badges = document.querySelectorAll('.badge');
    badges.forEach(badge => {
        badge.style.cursor = 'help';
        badge.title = badge.textContent + ' condition';
    });

    // Expandable sections (legacy support if needed, though we have new sections now)
    const expandButtons = document.querySelectorAll('.expand-btn');
    expandButtons.forEach(button => {
        button.addEventListener('click', function () {
            const content = this.nextElementSibling;
            if (content.style.display === 'none') {
                content.style.display = 'block';
                this.textContent = 'Show Less';
            } else {
                content.style.display = 'none';
                this.textContent = 'Show More';
            }
        });
    });

    console.log('Yakult Inventory System User Manual loaded successfully!');
});
