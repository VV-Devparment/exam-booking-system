﻿const API_BASE = '/api';
const stripe = Stripe('pk_test_51S7ZSCLSumIf2sloNewlIWsGz6Mbm4GnB8e3DQvX32MP99eneIFrhv5OxtL75yvvzoOa8etP8HHzVhs7zfAOZY8d00B8XsRw0o');

document.getElementById('bookingForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    e.stopPropagation();

    if (e.target.dataset.submitting === 'true') {
        return;
    }
    e.target.dataset.submitting = 'true';

    const submitBtn = e.target.querySelector('button[type="submit"]');
    const submitText = document.getElementById('submitText');
    const loadingSpinner = document.getElementById('loadingSpinner');

    submitBtn.disabled = true;
    submitText.textContent = 'Processing...';
    loadingSpinner.classList.remove('d-none');

    const formData = {
        studentFirstName: document.getElementById('firstName').value,
        studentLastName: document.getElementById('lastName').value,
        studentEmail: document.getElementById('email').value,
        studentPhone: document.getElementById('phone').value,

        // Нові авіаційні поля
        aircraftType: document.getElementById('aircraftType').value,
        checkRideType: document.getElementById('checkRideType').value,
        preferredAirport: document.getElementById('preferredAirport').value,
        searchRadius: parseInt(document.getElementById('searchRadius').value) || 50,
        willingToFly: document.getElementById('willingToFly').checked,

        // Старі поля для сумісності з PaymentController
        studentAddress: document.getElementById('preferredAirport').value,
        examType: document.getElementById('checkRideType').value,
        preferredDate: new Date().toISOString(),
        preferredTime: '10:00',
        specialRequirements: document.getElementById('additionalNotes')?.value || ''
    };

    try {
        const response = await fetch(`${API_BASE}/Payment/create-checkout-session`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        });

        if (response.ok) {
            const result = await response.json();

            window.location.href = result.url;
        } else {
            const error = await response.text();
            showError(error);
        }
    } catch (error) {
        showError('Network error. Please check your connection.');
    } finally {
        submitBtn.disabled = false;
        submitText.textContent = 'Proceed to Payment ($100)';
        loadingSpinner.classList.add('d-none');
        e.target.dataset.submitting = 'false';
    }
});

document.getElementById('examinerResponseForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const responseData = {
        bookingId: document.getElementById('bookingId').value,
        examinerEmail: document.getElementById('examinerEmail').value,
        examinerName: document.getElementById('examinerName').value,
        response: document.querySelector('input[name="response"]:checked').value,
        studentName: document.getElementById('studentName').value,
        studentEmail: document.getElementById('studentEmail').value,
        proposedDateTime: document.getElementById('proposedDateTime').value,
        responseMessage: document.getElementById('responseMessage').value
    };

    try {
        const response = await fetch(`${API_BASE}/Booking/examiner/respond`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(responseData)
        });

        const result = await response.json();
        const resultDiv = document.getElementById('examinerResponseResult');

        if (response.ok) {
            if (result.assigned) {
                resultDiv.innerHTML = `
                    <div class="alert alert-success">
                        <h5>✅ Success!</h5>
                        <p>${result.message}</p>
                    </div>
                `;
            } else {
                resultDiv.innerHTML = `
                    <div class="alert alert-warning">
                        <h5>⚠️ Not Assigned</h5>
                        <p>${result.message}</p>
                    </div>
                `;
            }
        } else {
            resultDiv.innerHTML = `
                <div class="alert alert-danger">
                    <h5>❌ Error</h5>
                    <p>${result.message || 'An error occurred'}</p>
                </div>
            `;
        }
    } catch (error) {
        document.getElementById('examinerResponseResult').innerHTML = `
            <div class="alert alert-danger">
                <h5>❌ Network Error</h5>
                <p>Could not connect to server</p>
            </div>
        `;
    }
});

function showSuccess(result) {
    document.getElementById('studentForm').classList.add('d-none');
    document.getElementById('successMessage').classList.remove('d-none');
    document.getElementById('bookingIdDisplay').textContent = result.bookingId;

    const examinersList = document.getElementById('examinersList');
    examinersList.innerHTML = '';

    if (result.examinersContacted && result.examinersContacted.length > 0) {
        result.examinersContacted.forEach(examiner => {
            const li = document.createElement('li');
            li.textContent = examiner;
            examinersList.appendChild(li);
        });
    }
}

function showError(error) {
    document.getElementById('studentForm').classList.add('d-none');
    document.getElementById('errorMessage').classList.remove('d-none');
    document.getElementById('errorText').textContent = error || 'An unexpected error occurred';
}

function createNewBooking() {
    document.getElementById('bookingForm').reset();
    document.getElementById('studentForm').classList.remove('d-none');
    document.getElementById('successMessage').classList.add('d-none');
    document.getElementById('errorMessage').classList.add('d-none');
}

function retryBooking() {
    document.getElementById('studentForm').classList.remove('d-none');
    document.getElementById('errorMessage').classList.add('d-none');
}

function showExaminerPanel() {
    const modal = new bootstrap.Modal(document.getElementById('examinerModal'));
    modal.show();
}

function showActiveBookings() {
    const modal = new bootstrap.Modal(document.getElementById('activeBookingsModal'));
    modal.show();
    loadActiveBookings();
}

async function loadActiveBookings() {
    const listDiv = document.getElementById('activeBookingsList');
    listDiv.innerHTML = '<div class="spinner-border"></div> Loading...';

    try {
        const response = await fetch(`${API_BASE}/Booking/active`);
        if (response.ok) {
            const bookings = await response.json();

            if (bookings.length === 0) {
                listDiv.innerHTML = '<p class="text-muted">No active bookings at the moment</p>';
                return;
            }

            let html = '<div class="table-responsive"><table class="table table-striped">';
            html += '<thead><tr><th>Booking ID</th><th>Student</th><th>Email</th><th>Exam Type</th><th>Status</th><th>Paid</th><th>Created</th></tr></thead><tbody>';

            bookings.forEach(booking => {
                const statusBadge = getStatusBadge(booking.status);
                const paidBadge = booking.isPaid ? '<span class="badge bg-success">Paid</span>' : '<span class="badge bg-warning">Pending</span>';
                html += `
                    <tr>
                        <td><code>${booking.bookingId}</code></td>
                        <td>${booking.studentName}</td>
                        <td>${booking.studentEmail}</td>
                        <td>${booking.examType}</td>
                        <td>${statusBadge}</td>
                        <td>${paidBadge}</td>
                        <td>${new Date(booking.createdAt).toLocaleString()}</td>
                    </tr>
                `;
            });

            html += '</tbody></table></div>';
            listDiv.innerHTML = html;
        } else {
            listDiv.innerHTML = '<div class="alert alert-danger">Failed to load bookings</div>';
        }
    } catch (error) {
        listDiv.innerHTML = '<div class="alert alert-danger">Network error</div>';
    }
}

function getStatusBadge(status) {
    const badges = {
        'Created': '<span class="badge bg-secondary">Created</span>',
        'PaymentPending': '<span class="badge bg-warning">Payment Pending</span>',
        'PaymentConfirmed': '<span class="badge bg-info">Payment Confirmed</span>',
        'ExaminersContacted': '<span class="badge bg-info">Examiners Contacted</span>',
        'ExaminerAssigned': '<span class="badge bg-success">Examiner Assigned</span>',
        'Scheduled': '<span class="badge bg-primary">Scheduled</span>',
        'Completed': '<span class="badge bg-dark">Completed</span>',
        'Cancelled': '<span class="badge bg-danger">Cancelled</span>'
    };
    return badges[status] || `<span class="badge bg-secondary">${status}</span>`;
}

const tomorrow = new Date();
tomorrow.setDate(tomorrow.getDate() + 1);
tomorrow.setHours(10, 0, 0, 0);
const dateStr = tomorrow.toISOString().slice(0, 16);