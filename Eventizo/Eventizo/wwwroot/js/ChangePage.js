document.addEventListener("DOMContentLoaded", function () {
    const rowsPerPage = 5;
    const tbody = document.getElementById("product-table-body");
    const pagination = document.getElementById("pagination");
    const rows = Array.from(tbody.querySelectorAll("tr"));
    let currentPage = 1;

    function renderTablePage(page) {
        const start = (page - 1) * rowsPerPage;
        const end = start + rowsPerPage;
        rows.forEach((row, index) => {
            row.style.display = index >= start && index < end ? "" : "none";
        });
    }

    function renderPagination() {
        const pageCount = Math.ceil(rows.length / rowsPerPage);
        pagination.innerHTML = "";

        const prevItem = document.createElement("li");
        prevItem.className = `page-item ${currentPage === 1 ? 'disabled' : ''}`;
        prevItem.innerHTML = `<a class="page-link" href="#">Trước</a>`;
        prevItem.onclick = () => {
            if (currentPage > 1) {
                currentPage--;
                renderTablePage(currentPage);
                renderPagination();
            }
        };
        pagination.appendChild(prevItem);

        for (let i = 1; i <= pageCount; i++) {
            const pageItem = document.createElement("li");
            pageItem.className = `page-item ${i === currentPage ? 'active' : ''}`;
            pageItem.innerHTML = `<a class="page-link" href="#">${i}</a>`;
            pageItem.onclick = () => {
                currentPage = i;
                renderTablePage(currentPage);
                renderPagination();
            };
            pagination.appendChild(pageItem);
        }

        const nextItem = document.createElement("li");
        nextItem.className = `page-item ${currentPage === pageCount ? 'disabled' : ''}`;
        nextItem.innerHTML = `<a class="page-link" href="#">Sau</a>`;
        nextItem.onclick = () => {
            if (currentPage < pageCount) {
                currentPage++;
                renderTablePage(currentPage);
                renderPagination();
            }
        };
        pagination.appendChild(nextItem);
    }

    renderTablePage(currentPage);
    renderPagination();
});