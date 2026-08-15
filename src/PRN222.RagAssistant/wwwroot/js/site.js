// PRN222 RAG Assistant - Interactive Script (Humata Inspired Engine)

document.addEventListener('DOMContentLoaded', () => {
    // 1. Top Loading Bar on Navigation
    initTopLoadingBar();

    // 2. Typewriter Animation for Hero Title
    initTypewriter();

    // 3. Showcase Auto-Slider Carousel (4.5s)
    initShowcaseSlider();

    // 4. FAQ Accordion Toggle
    initFaqAccordion();

    // 5. Testimonials Auto-Slider (5.0s)
    initTestimonialsSlider();

    // 6. Form Submission Buttons & Interactive Micro-Interactions
    initFormInteractions();

    // 7. Clipboard Copy Helper
    initClipboardHelper();
});

/* ==========================================================================
   1. Top Navigation Loading Bar (Facebook / YouTube / Linear Style)
   ========================================================================== */
function initTopLoadingBar() {
    let bar = document.getElementById('top-loading-bar');
    if (!bar) {
        bar = document.createElement('div');
        bar.id = 'top-loading-bar';
        document.body.prepend(bar);
    }

    // Complete on initial DOM ready
    bar.style.width = '100%';
    bar.classList.add('active');
    setTimeout(() => {
        bar.style.opacity = '0';
        setTimeout(() => {
            bar.style.width = '0%';
            bar.classList.remove('active');
        }, 250);
    }, 200);

    // Animate on clicking internal navigation links
    document.addEventListener('click', (e) => {
        const link = e.target.closest('a');
        if (!link) return;

        const href = link.getAttribute('href');
        const target = link.getAttribute('target');

        // Only trigger for normal same-origin internal links
        if (href && !href.startsWith('#') && !href.startsWith('javascript:') && (!target || target === '_self')) {
            const url = new URL(link.href, window.location.origin);
            if (url.origin === window.location.origin && url.pathname !== window.location.pathname) {
                bar.classList.add('active');
                bar.style.opacity = '1';
                bar.style.width = '30%';
                setTimeout(() => { bar.style.width = '70%'; }, 150);
            }
        }
    });

    // Animate on form submits
    document.addEventListener('submit', (e) => {
        const form = e.target;
        if (form && !e.defaultPrevented) {
            bar.classList.add('active');
            bar.style.opacity = '1';
            bar.style.width = '45%';
            setTimeout(() => { bar.style.width = '85%'; }, 200);
        }
    });
}

/* ==========================================================================
   2. Modern Toast Notification System
   ========================================================================== */
window.Toast = {
    show(message, type = 'info', duration = 3500) {
        let container = document.querySelector('.toast-container-modern');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container-modern';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `toast-item toast-${type}`;

        let iconSvg = '';
        if (type === 'success') {
            iconSvg = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>';
        } else if (type === 'error') {
            iconSvg = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>';
        } else {
            iconSvg = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';
        }

        toast.innerHTML = `
            <span class="toast-item-icon">${iconSvg}</span>
            <span class="toast-item-msg">${escapeHtml(message)}</span>
        `;

        container.appendChild(toast);

        // Entrance animation
        requestAnimationFrame(() => {
            toast.classList.add('show');
        });

        // Auto dismiss
        setTimeout(() => {
            toast.classList.remove('show');
            toast.classList.add('hiding');
            setTimeout(() => {
                toast.remove();
            }, 250);
        }, duration);
    }
};

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/* ==========================================================================
   3. Typewriter Engine
   ========================================================================== */
function initTypewriter() {
    const dynamicElem = document.getElementById('typewriterDynamic');
    if (!dynamicElem) return;

    const phrases = [
        "tài liệu PDF giáo trình",
        "đề cương Syllabus FLM",
        "mã nguồn C# 12 & .NET 10",
        "cơ sở dữ liệu Vector pgvector",
        "câu hỏi bài tập PRN222"
    ];

    let phraseIndex = 0;
    let charIndex = 0;
    let isDeleting = false;
    let typingSpeed = 80;

    function typeLoop() {
        const currentPhrase = phrases[phraseIndex];

        if (isDeleting) {
            dynamicElem.textContent = currentPhrase.substring(0, charIndex - 1);
            charIndex--;
            typingSpeed = 40;
        } else {
            dynamicElem.textContent = currentPhrase.substring(0, charIndex + 1);
            charIndex++;
            typingSpeed = 80;
        }

        if (!isDeleting && charIndex === currentPhrase.length) {
            typingSpeed = 2200;
            isDeleting = true;
        } else if (isDeleting && charIndex === 0) {
            isDeleting = false;
            phraseIndex = (phraseIndex + 1) % phrases.length;
            typingSpeed = 400;
        }

        setTimeout(typeLoop, typingSpeed);
    }

    typeLoop();
}

/* ==========================================================================
   4. Showcase Auto-Slider Carousel
   ========================================================================== */
function initShowcaseSlider() {
    const track = document.getElementById('showcaseSliderTrack');
    const dots = document.querySelectorAll('.showcase-dot');
    if (!track || dots.length === 0) return;

    let currentIndex = 0;
    const totalSlides = dots.length;
    let timer = null;

    function goToSlide(index) {
        currentIndex = index;
        track.style.transform = `translateX(-${currentIndex * 100}%)`;
        dots.forEach((d, i) => {
            if (i === currentIndex) d.classList.add('active');
            else d.classList.remove('active');
        });
    }

    function startAutoSlide() {
        if (timer) clearInterval(timer);
        timer = setInterval(() => {
            let nextIndex = (currentIndex + 1) % totalSlides;
            goToSlide(nextIndex);
        }, 4500);
    }

    dots.forEach((dot, i) => {
        dot.addEventListener('click', () => {
            goToSlide(i);
            startAutoSlide();
        });
    });

    const windowContainer = document.querySelector('.showcase-window');
    if (windowContainer) {
        windowContainer.addEventListener('mouseenter', () => {
            if (timer) clearInterval(timer);
        });
        windowContainer.addEventListener('mouseleave', () => {
            startAutoSlide();
        });
    }

    startAutoSlide();
}

/* ==========================================================================
   5. FAQ Accordion Toggle
   ========================================================================== */
function initFaqAccordion() {
    const faqItems = document.querySelectorAll('.faq-item');
    if (faqItems.length === 0) return;

    faqItems.forEach(item => {
        const questionBtn = item.querySelector('.faq-question');
        if (!questionBtn) return;

        questionBtn.addEventListener('click', () => {
            const isActive = item.classList.contains('active');
            
            faqItems.forEach(other => {
                if (other !== item) other.classList.remove('active');
            });

            if (isActive) {
                item.classList.remove('active');
            } else {
                item.classList.add('active');
            }
        });
    });
}

/* ==========================================================================
   6. Testimonials Auto-Slider
   ========================================================================== */
function initTestimonialsSlider() {
    const quoteElem = document.getElementById('testiQuote');
    const nameElem = document.getElementById('testiName');
    const roleElem = document.getElementById('testiRole');
    const avatarElem = document.getElementById('testiAvatar');
    const dots = document.querySelectorAll('.testimonial-dot');

    if (!quoteElem || dots.length === 0) return;

    const testimonials = [
        {
            quote: "“Giao diện thân thiện và tốc độ phản hồi cực nhanh, giúp mình ôn tập môn PRN222 và tra cứu nhanh các khái niệm C# nâng cao chỉ trong vài giây khi từng phút ôn thi đều quý giá.”",
            name: "Trần Minh Hoàng",
            role: "Sinh viên K18 Ngành Kỹ thuật Phần mềm — ĐH FPT",
            avatar: "/images/avatars/avatar-1.png"
        },
        {
            quote: "“Hệ thống RAG trích xuất chính xác từng số trang từ tài liệu PDF và slide bài giảng. Giảng viên và sinh viên đối soát kiến thức cực kỳ minh bạch, không còn tình trạng AI trả lời mơ hồ.”",
            name: "Đỗ Hoàng Long",
            role: "Trưởng Bộ môn Phát triển Ứng dụng .NET — ĐH FPT",
            avatar: "/images/avatars/avatar-2.png"
        },
        {
            quote: "“Tính năng tìm kiếm ngữ nghĩa qua pgvector giúp mình tìm đúng các đoạn mã nguồn và lưu ý trong đề cương môn học mà các công cụ tìm kiếm từ khóa truyền thống không thể tìm ra.”",
            name: "Nguyễn Hà My",
            role: "Sinh viên K17 ĐH FPT — Đạt điểm A môn PRN222",
            avatar: "/images/avatars/avatar-3.png"
        }
    ];

    let current = 0;
    let timer = null;

    function renderTestimonial(index) {
        current = index;
        const data = testimonials[current];
        
        quoteElem.style.opacity = 0;
        setTimeout(() => {
            quoteElem.textContent = data.quote;
            nameElem.textContent = data.name;
            roleElem.textContent = data.role;
            if (avatarElem) avatarElem.src = data.avatar;
            quoteElem.style.opacity = 1;
        }, 150);

        dots.forEach((d, i) => {
            if (i === current) d.classList.add('active');
            else d.classList.remove('active');
        });
    }

    function startAuto() {
        if (timer) clearInterval(timer);
        timer = setInterval(() => {
            let next = (current + 1) % testimonials.length;
            renderTestimonial(next);
        }, 5000);
    }

    dots.forEach((dot, i) => {
        dot.addEventListener('click', () => {
            renderTestimonial(i);
            startAuto();
        });
    });

    startAuto();
}

/* ==========================================================================
   7. Form Interactions & Button Loading States
   ========================================================================== */
function initFormInteractions() {
    document.querySelectorAll('form').forEach(form => {
        form.addEventListener('submit', (e) => {
            if (form.checkValidity && !form.checkValidity()) {
                return;
            }
            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn && !submitBtn.disabled) {
                submitBtn.classList.add('loading');
            }
        });
    });
}

/* ==========================================================================
   8. Clipboard Copy Helper with Toast
   ========================================================================== */
function initClipboardHelper() {
    document.addEventListener('click', (e) => {
        const copyTrigger = e.target.closest('[data-copy]');
        if (!copyTrigger) return;

        const textToCopy = copyTrigger.getAttribute('data-copy');
        if (textToCopy && navigator.clipboard) {
            navigator.clipboard.writeText(textToCopy).then(() => {
                const label = copyTrigger.getAttribute('data-copy-label') || 'Đã sao chép vào bộ nhớ tạm!';
                window.Toast.show(label, 'success', 2500);
            }).catch(() => {
                window.Toast.show('Không thể sao chép!', 'error', 2500);
            });
        }
    });
}
