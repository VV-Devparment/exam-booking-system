const API_BASE = '/api';
let allBookings = [];
let isAdminLoggedIn = false;

// Завантаження адмін панелі
async function loadAdminDashboard() {
    const adminContent = document.getElementById('adminContent');
    if (!adminContent) return;

    // Створюємо HTML для адмін панелі
    adminContent.innerHTML = `
        <div class="row mb-4">
            <div class="col-md-3">
                <div class="stat-card">
                    <h6>Total Bookings</h6>
                    <h3 id="totalBookings">0</h3>
                </div>
            </div>
            <div class="col-md-3">
                <div class="stat-card">
                    <h6>Assigned</h6>
                    <h3 id="assignedBookings">0</h3>
                </div>
            </div>
            <div class="col-md-3">
                <div class="stat-card">
                    <h6>Pending</h6>
                    <h3 id="pendingBookings">0</h3>
                </div>
            </div>
            <div class="col-md-3">
                <div class="stat-card">
                    <h6>Total Revenue</h6>
                    <h3 id="totalRevenue">$0</h3>
                </div>
            </div>
        </div>

        <div class="card">
            <div class="card-header">
                <div class="row">
                    <div class="col-md-6">
                        <h5 class="mb-0">Booking Management</h5>
                    </div>
                    <div class="col-md-6 text-end">
                        <button class="btn btn-primary btn-sm" onclick="refreshBookings()">
                            <i class="bi bi-arrow-clockwise"></i> Refresh
                        </button>
                        <button class="btn btn-success btn-sm" onclick="exportData()">
                            <i class="bi bi-download"></i> Export CSV
                        </button>
                    </div>
                </div>
            </div>
            <div class="card-body">
                <div class="row mb-3">
                    <div class="col-md-4">
                        <input type="text" id="searchFilter" class="form-control" 
                               placeholder="Search by ID, name or email..." onkeyup="filterBookings()">
                    </div>
                    <div class="col-md-4">
                        <select id="statusFilter" class="form-select" onchange="filterBookings()">
                            <option value="">All Status</option>
                            <option value="Created">Created</option>
                            <option value="PaymentConfirmed">Payment Confirmed</option>
                            <option value="ExaminersContacted">Examiners Contacted</option>
                            <option value="ExaminerAssigned">Examiner Assigned</option>
                            <option value="Cancelled">Cancelled</option>
                        </select>
                    </div>
                    <div class="col-md-4">
                        <input type="date" id="dateFilter" class="form-control" onchange="filterBookings()">
                    </div>
                </div>
                <div class="table-responsive">
                    <table class="table table-hover">
                        <thead>
                            <tr>
                                <th>Booking ID</th>
                                <th>Student</th>
                                <th>Email</th>
                                <th>Exam Type</th>
                                <th>Status</th>
                                <th>Payment</th>
                                <th>Examiner</th>
                                <th>Created</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody id="bookingsTableBody">
                            <tr>
                                <td colspan="9" class="text-center">Loading...</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    `;

    // Завантажуємо дані
    await loadBookings();
    await loadStatistics();
}

// Робимо функцію глобальною
window.loadAdminDashboard = loadAdminDashboard;

// Завантаження статистики
async function loadStatistics() {
    try {
        const response = await fetch(`${API_BASE}/Admin/dashboard-stats`);
        if (response.ok) {
            const stats = await response.json();

            if (document.getElementById('totalBookings')) {
                document.getElementById('totalBookings').textContent = stats.totalBookings || '0';
            }
            if (document.getElementById('assignedBookings')) {
                document.getElementById('assignedBookings').textContent = stats.assignedBookings || '0';
            }
            if (document.getElementById('pendingBookings')) {
                document.getElementById('pendingBookings').textContent = stats.pendingBookings || '0';
            }
            if (document.getElementById('totalRevenue')) {
                document.getElementById('totalRevenue').textContent = `$${stats.totalRevenue || '0'}`;
            }
        }
    } catch (error) {
        console.error('Error loading statistics:', error);
    }
}

// Завантаження букінгів
async function loadBookings() {
    try {
        const response = await fetch(`${API_BASE}/Admin/bookings`);
        if (response.ok) {
            allBookings = await response.json();
            displayBookings(allBookings);
        }
    } catch (error) {
        console.error('Error loading bookings:', error);
    }
}

// Відображення букінгів
function displayBookings(bookings) {
    const tbody = document.getElementById('bookingsTableBody');
    if (!tbody) return;

    if (!bookings || bookings.length === 0) {
        tbody.innerHTML = '<tr><td colspan="9" class="text-center">No bookings found</td></tr>';
        return;
    }

    tbody.innerHTML = bookings.map(booking => `
        <tr>
            <td><code>${booking.bookingId}</code></td>
            <td>${booking.studentName}</td>
            <td>${booking.studentEmail}</td>
            <td>${booking.examType || '-'}</td>
            <td>${getStatusBadge(booking.status)}</td>
            <td>${booking.isPaid ?
            '<span class="badge bg-success">Paid</span>' :
            '<span class="badge bg-warning">Pending</span>'}</td>
            <td>${booking.assignedExaminerName || '-'}</td>
            <td>${new Date(booking.createdAt).toLocaleDateString()}</td>
            <td>
                <button class="btn btn-sm btn-info" onclick="viewDetails('${booking.bookingId}')" title="View Details">
                    <i class="bi bi-eye"></i>
                </button>
                ${!booking.assignedExaminerName && booking.isPaid ?
            `<button class="btn btn-sm btn-warning" onclick="processRefund('${booking.bookingId}')" title="Process Refund">
                        <i class="bi bi-cash"></i>
                    </button>` : ''}
            </td>
        </tr>
    `).join('');
}

// Отримання badge для статусу
function getStatusBadge(status) {
    const badges = {
        'Created': '<span class="badge bg-secondary">Created</span>',
        'PaymentPending': '<span class="badge bg-warning">Payment Pending</span>',
        'PaymentConfirmed': '<span class="badge bg-info">Payment Confirmed</span>',
        'ExaminersContacted': '<span class="badge bg-info">Examiners Contacted</span>',
        'ExaminerAssigned': '<span class="badge bg-success">Examiner Assigned</span>',
        'Scheduled': '<span class="badge bg-primary">Scheduled</span>',
        'Completed': '<span class="badge bg-dark">Completed</span>',
        'Cancelled': '<span class="badge bg-danger">Cancelled</span>',
        'Refunded': '<span class="badge bg-secondary">Refunded</span>'
    };
    return badges[status] || `<span class="badge bg-secondary">${status}</span>`;
}

// Фільтрація букінгів
function filterBookings() {
    const searchFilter = document.getElementById('searchFilter');
    const statusFilter = document.getElementById('statusFilter');
    const dateFilter = document.getElementById('dateFilter');

    let filtered = allBookings;

    if (statusFilter && statusFilter.value) {
        filtered = filtered.filter(b => b.status === statusFilter.value);
    }

    if (dateFilter && dateFilter.value) {
        filtered = filtered.filter(b =>
            new Date(b.createdAt).toDateString() === new Date(dateFilter.value).toDateString()
        );
    }

    if (searchFilter && searchFilter.value) {
        const search = searchFilter.value.toLowerCase();
        filtered = filtered.filter(b =>
            b.bookingId.toLowerCase().includes(search) ||
            b.studentName.toLowerCase().includes(search) ||
            b.studentEmail.toLowerCase().includes(search)
        );
    }

    displayBookings(filtered);
}

// Оновлення даних
function refreshBookings() {
    loadBookings();
    loadStatistics();
}

// Експорт даних в CSV
function exportData() {
    const headers = ['Booking ID', 'Student Name', 'Email', 'Exam Type', 'Status', 'Payment', 'Examiner', 'Created'];
    const rows = allBookings.map(b => [
        b.bookingId,
        b.studentName,
        b.studentEmail,
        b.examType || '',
        b.status,
        b.isPaid ? 'Paid' : 'Pending',
        b.assignedExaminerName || '',
        new Date(b.createdAt).toLocaleDateString()
    ]);

    let csv = headers.join(',') + '\n';
    rows.forEach(row => {
        csv += row.map(cell => `"${cell}"`).join(',') + '\n';
    });

    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `bookings_${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
}

// Перегляд деталей букінгу
function viewDetails(bookingId) {
    alert(`Details for booking ${bookingId}`);
    // TODO: Додати модальне вікно з деталями
}

// Обробка повернення коштів
async function processRefund(bookingId) {
    if (!confirm(`Are you sure you want to process a refund for booking ${bookingId}?`)) return;

    try {
        const response = await fetch(`${API_BASE}/Admin/process-refund/${bookingId}`, {
            method: 'POST'
        });

        if (response.ok) {
            alert('Refund processed successfully');
            refreshBookings();
        } else {
            alert('Failed to process refund');
        }
    } catch (error) {
        console.error('Error processing refund:', error);
        alert('Error processing refund');
    }
}