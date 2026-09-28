document.addEventListener("DOMContentLoaded", function () {

     
const elements = document.querySelectorAll(".reveal");

if ("IntersectionObserver" in window) {

    const observer = new IntersectionObserver(
        function (entries) {

            entries.forEach(function (entry) {

                if (entry.isIntersecting) {

                    entry.target.classList.add("visible");

                    observer.unobserve(entry.target);
                }

            });

        },
        {
            threshold: 0.12
        }
    );

    elements.forEach(function (element) {
        observer.observe(element);
    });

} else {

    elements.forEach(function (element) {
        element.classList.add("visible");
    });

}
 

});
