// PRN222 RAG Assistant - Interactive Script (Humata Inspired Engine)

document.addEventListener('DOMContentLoaded', () => {
    // 1. Typewriter Animation for Hero Title
    initTypewriter();

    // 2. Showcase Auto-Slider Carousel (4.5s)
    initShowcaseSlider();

    // 3. FAQ Accordion Toggle
    initFaqAccordion();

    // 4. Testimonials Auto-Slider (5.0s)
    initTestimonialsSlider();
});

/* ==========================================================================
   1. Typewriter Engine
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
            // Finished typing, pause before deleting
            typingSpeed = 2200;
            isDeleting = true;
        } else if (isDeleting && charIndex === 0) {
            // Finished deleting, move to next phrase
            isDeleting = false;
            phraseIndex = (phraseIndex + 1) % phrases.length;
            typingSpeed = 400;
        }

        setTimeout(typeLoop, typingSpeed);
    }

    typeLoop();
}

/* ==========================================================================
   2. Showcase Auto-Slider Carousel
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
        }, 4500); // 4.5s auto transition
    }

    dots.forEach((dot, i) => {
        dot.addEventListener('click', () => {
            goToSlide(i);
            startAutoSlide(); // Reset timer on manual click
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
   3. FAQ Accordion Toggle
   ========================================================================== */
function initFaqAccordion() {
    const faqItems = document.querySelectorAll('.faq-item');
    if (faqItems.length === 0) return;

    faqItems.forEach(item => {
        const questionBtn = item.querySelector('.faq-question');
        if (!questionBtn) return;

        questionBtn.addEventListener('click', () => {
            const isActive = item.classList.contains('active');
            
            // Close other items
            faqItems.forEach(other => {
                if (other !== item) other.classList.remove('active');
            });

            // Toggle current
            if (isActive) {
                item.classList.remove('active');
            } else {
                item.classList.add('active');
            }
        });
    });
}

/* ==========================================================================
   4. Testimonials Auto-Slider
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
