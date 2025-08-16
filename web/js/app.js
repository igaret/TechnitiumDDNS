// Global variables
let currentUser = null;
let apiBaseUrl = '/api';

// DOM Elements
document.addEventListener('DOMContentLoaded', function() {
    // Initialize the application
    init();
});

// Initialize the application
function init() {
    // Check if user is logged in
    checkAuthStatus();
    
    // Set up event listeners
    setupEventListeners();
}

// Check authentication status
function checkAuthStatus() {
    const token = localStorage.getItem('token');
    const userData = localStorage.getItem('user');
    
    if (token && userData) {
        try {
            currentUser = JSON.parse(userData);
            updateUIForLoggedInUser();
        } catch (error) {
            console.error('Error parsing user data:', error);
            logout();
        }
    } else {
        updateUIForLoggedOutUser();
    }
}

// Set up event listeners
function setupEventListeners() {
    // Navigation links
    document.querySelectorAll('nav a').forEach(link => {
        link.addEventListener('click', handleNavigation);
    });
    
    // Login form
    const loginForm = document.getElementById('login-form');
    if (loginForm) {
        loginForm.addEventListener('submit', handleLogin);
    }
    
    // Register form
    const registerForm = document.getElementById('register-form');
    if (registerForm) {
        registerForm.addEventListener('submit', handleRegister);
    }
    
    // Logout button
    const logoutBtn = document.getElementById('logout-btn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', logout);
    }
    
    // Add domain form
    const addDomainForm = document.getElementById('add-domain-form');
    if (addDomainForm) {
        addDomainForm.addEventListener('submit', handleAddDomain);
    }
    
    // Update profile form
    const updateProfileForm = document.getElementById('update-profile-form');
    if (updateProfileForm) {
        updateProfileForm.addEventListener('submit', handleUpdateProfile);
    }
    
    // Add API key form
    const addApiKeyForm = document.getElementById('add-api-key-form');
    if (addApiKeyForm) {
        addApiKeyForm.addEventListener('submit', handleAddApiKey);
    }
    
    // Subscribe buttons
    document.querySelectorAll('.subscribe-btn').forEach(btn => {
        btn.addEventListener('click', handleSubscribe);
    });
}

// Handle navigation
function handleNavigation(e) {
    e.preventDefault();
    const target = e.target.getAttribute('href');
    
    // Hide all sections
    document.querySelectorAll('main > section').forEach(section => {
        section.classList.add('hidden');
    });
    
    // Show target section
    const targetSection = document.querySelector(target);
    if (targetSection) {
        targetSection.classList.remove('hidden');
    }
    
    // Update active nav link
    document.querySelectorAll('nav a').forEach(link => {
        link.classList.remove('active');
    });
    e.target.classList.add('active');
    
    // Load section-specific data
    if (target === '#domains') {
        loadDomains();
    } else if (target === '#profile') {
        loadProfile();
    } else if (target === '#api-keys') {
        loadApiKeys();
    } else if (target === '#billing') {
        loadBilling();
    }
}

// Handle login
async function handleLogin(e) {
    e.preventDefault();
    
    const usernameOrEmail = document.getElementById('login-username').value;
    const password = document.getElementById('login-password').value;
    
    try {
        showLoading('login-btn');
        
        const response = await fetch(`${apiBaseUrl}/user/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                usernameOrEmail,
                password
            })
        });
        
        const data = await response.json();
        
        if (response.ok) {
            // Save token and user data
            localStorage.setItem('token', data.token);
            localStorage.setItem('user', JSON.stringify({
                id: data.userId,
                username: data.username,
                email: data.email,
                plan: data.plan,
                isAdmin: data.isAdmin
            }));
            
            currentUser = {
                id: data.userId,
                username: data.username,
                email: data.email,
                plan: data.plan,
                isAdmin: data.isAdmin
            };
            
            updateUIForLoggedInUser();
            showMessage('login-message', 'Login successful!', 'success');
            
            // Redirect to domains page
            document.querySelector('nav a[href="#domains"]').click();
        } else {
            showMessage('login-message', data.error || 'Login failed. Please check your credentials.', 'danger');
        }
    } catch (error) {
        console.error('Error during login:', error);
        showMessage('login-message', 'An error occurred during login. Please try again.', 'danger');
    } finally {
        hideLoading('login-btn');
    }
}

// Handle register
async function handleRegister(e) {
    e.preventDefault();
    
    const username = document.getElementById('register-username').value;
    const email = document.getElementById('register-email').value;
    const password = document.getElementById('register-password').value;
    const confirmPassword = document.getElementById('register-confirm-password').value;
    
    // Validate passwords match
    if (password !== confirmPassword) {
        showMessage('register-message', 'Passwords do not match.', 'danger');
        return;
    }
    
    try {
        showLoading('register-btn');
        
        const response = await fetch(`${apiBaseUrl}/user/register`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                username,
                email,
                password
            })
        });
        
        const data = await response.json();
        
        if (response.ok) {
            showMessage('register-message', 'Registration successful! Please check your email to verify your account.', 'success');
            
            // Clear form
            document.getElementById('register-form').reset();
            
            // Redirect to login page
            document.querySelector('nav a[href="#login"]').click();
        } else {
            showMessage('register-message', data.error || 'Registration failed. Please try again.', 'danger');
        }
    } catch (error) {
        console.error('Error during registration:', error);
        showMessage('register-message', 'An error occurred during registration. Please try again.', 'danger');
    } finally {
        hideLoading('register-btn');
    }
}

// Handle logout
function logout() {
    // Clear local storage
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    
    // Reset current user
    currentUser = null;
    
    // Update UI
    updateUIForLoggedOutUser();
    
    // Redirect to home page
    document.querySelector('nav a[href="#home"]').click();
}

// Load domains
async function loadDomains() {
    if (!currentUser) return;
    
    try {
        showLoading('domains-container');
        
        const response = await fetch(`${apiBaseUrl}/dynamicdns/list?userId=${currentUser.id}`, {
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const data = await response.json();
        
        if (response.ok) {
            const domainsContainer = document.getElementById('domains-list');
            
            if (data.domains && data.domains.length > 0) {
                let html = '<div class="table-container"><table>';
                html += '<thead><tr><th>Domain</th><th>IPv4</th><th>IPv6</th><th>Last Updated</th><th>Actions</th></tr></thead>';
                html += '<tbody>';
                
                data.domains.forEach(domain => {
                    html += `<tr>
                        <td>${domain.domain}</td>
                        <td>${domain.ipv4 || '-'}</td>
                        <td>${domain.ipv6 || '-'}</td>
                        <td>${new Date(domain.lastUpdated).toLocaleString()}</td>
                        <td>
                            <button class="btn btn-secondary btn-sm" onclick="showDomainDetails(${domain.id})">Details</button>
                            <button class="btn btn-danger btn-sm" onclick="deleteDomain(${domain.id})">Delete</button>
                        </td>
                    </tr>`;
                });
                
                html += '</tbody></table></div>';
                domainsContainer.innerHTML = html;
            } else {
                domainsContainer.innerHTML = '<div class="alert alert-info">You don\'t have any domains yet. Add your first domain below.</div>';
            }
        } else {
            showMessage('domains-message', data.error || 'Failed to load domains.', 'danger');
        }
    } catch (error) {
        console.error('Error loading domains:', error);
        showMessage('domains-message', 'An error occurred while loading domains.', 'danger');
    } finally {
        hideLoading('domains-container');
    }
}

// Handle add domain
async function handleAddDomain(e) {
    e.preventDefault();
    
    if (!currentUser) return;
    
    const domainName = document.getElementById('domain-name').value;
    const zoneName = document.getElementById('zone-name').value;
    
    try {
        showLoading('add-domain-btn');
        
        const response = await fetch(`${apiBaseUrl}/dynamicdns/create`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            },
            body: JSON.stringify({
                userId: currentUser.id,
                domainName,
                zoneName
            })
        });
        
        const data = await response.json();
        
        if (response.ok) {
            showMessage('add-domain-message', 'Domain added successfully!', 'success');
            
            // Clear form
            document.getElementById('add-domain-form').reset();
            
            // Reload domains
            loadDomains();
        } else {
            showMessage('add-domain-message', data.error || 'Failed to add domain.', 'danger');
        }
    } catch (error) {
        console.error('Error adding domain:', error);
        showMessage('add-domain-message', 'An error occurred while adding domain.', 'danger');
    } finally {
        hideLoading('add-domain-btn');
    }
}

// Show domain details
async function showDomainDetails(domainId) {
    if (!currentUser) return;
    
    try {
        const response = await fetch(`${apiBaseUrl}/dynamicdns/list?userId=${currentUser.id}`, {
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const data = await response.json();
        
        if (response.ok) {
            const domain = data.domains.find(d => d.id === domainId);
            
            if (domain) {
                const detailsHtml = `
                    <div class="card">
                        <div class="card-header">
                            <h3>${domain.domain}</h3>
                            <button class="btn btn-sm btn-secondary" onclick="closeDetails()">Close</button>
                        </div>
                        <div class="card-body">
                            <p><strong>IPv4:</strong> ${domain.ipv4 || '-'}</p>
                            <p><strong>IPv6:</strong> ${domain.ipv6 || '-'}</p>
                            <p><strong>Created:</strong> ${new Date(domain.created).toLocaleString()}</p>
                            <p><strong>Last Updated:</strong> ${new Date(domain.lastUpdated).toLocaleString()}</p>
                            <p><strong>Update Count:</strong> ${domain.updateCount}</p>
                            <p><strong>Last Update Status:</strong> ${domain.lastUpdateStatus || '-'}</p>
                            <p><strong>Last Update Attempt:</strong> ${domain.lastUpdateAttempt ? new Date(domain.lastUpdateAttempt).toLocaleString() : '-'}</p>
                            <p><strong>Notes:</strong> ${domain.notes || '-'}</p>
                            
                            <div class="mt-3">
                                <h4>Update Token</h4>
                                <div class="form-group">
                                    <input type="text" class="form-control" value="${domain.updateToken}" readonly>
                                </div>
                                <button class="btn btn-warning" onclick="regenerateToken(${domain.id})">Regenerate Token</button>
                            </div>
                            
                            <div class="mt-3">
                                <h4>Update URL</h4>
                                <div class="form-group">
                                    <input type="text" class="form-control" value="${window.location.origin}${domain.updateUrl}" readonly>
                                </div>
                                <p class="mt-2">Use this URL to update your IP address. You can also append <code>&ipv4=x.x.x.x</code> or <code>&ipv6=x:x:x:x:x:x:x:x</code> to specify IP addresses.</p>
                            </div>
                            
                            <div class="mt-3">
                                <h4>Example Update Commands</h4>
                                <pre>curl "${window.location.origin}${domain.updateUrl}"</pre>
                                <pre>wget -q -O - "${window.location.origin}${domain.updateUrl}"</pre>
                            </div>
                        </div>
                    </div>
                `;
                
                const detailsContainer = document.getElementById('domain-details');
                detailsContainer.innerHTML = detailsHtml;
                detailsContainer.classList.remove('hidden');
                
                document.getElementById('domains-list').classList.add('hidden');
                document.getElementById('add-domain-container').classList.add('hidden');
            }
        }
    } catch (error) {
        console.error('Error loading domain details:', error);
        showMessage('domains-message', 'An error occurred while loading domain details.', 'danger');
    }
}

// Close domain details
function closeDetails() {
    document.getElementById('domain-details').classList.add('hidden');
    document.getElementById('domains-list').classList.remove('hidden');
    document.getElementById('add-domain-container').classList.remove('hidden');
}

// Delete domain
async function deleteDomain(domainId) {
    if (!currentUser) return;
    
    if (!confirm('Are you sure you want to delete this domain? This action cannot be undone.')) {
        return;
    }
    
    try {
        const response = await fetch(`${apiBaseUrl}/dynamicdns/${domainId}?userId=${currentUser.id}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const data = await response.json();
        
        if (response.ok) {
            showMessage('domains-message', 'Domain deleted successfully!', 'success');
            loadDomains();
        } else {
            showMessage('domains-message', data.error || 'Failed to delete domain.', 'danger');
        }
    } catch (error) {
        console.error('Error deleting domain:', error);
        showMessage('domains-message', 'An error occurred while deleting domain.', 'danger');
    }
}

// Regenerate token
async function regenerateToken(domainId) {
    if (!currentUser) return;
    
    if (!confirm('Are you sure you want to regenerate the update token? This will invalidate the current token.')) {
        return;
    }
    
    try {
        const response = await fetch(`${apiBaseUrl}/dynamicdns/regenerate-token/${domainId}?userId=${currentUser.id}`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const data = await response.json();
        
        if (response.ok) {
            showMessage('domains-message', 'Token regenerated successfully!', 'success');
            showDomainDetails(domainId);
        } else {
            showMessage('domains-message', data.error || 'Failed to regenerate token.', 'danger');
        }
    } catch (error) {
        console.error('Error regenerating token:', error);
        showMessage('domains-message', 'An error occurred while regenerating token.', 'danger');
    }
}

// Load profile
function loadProfile() {
    if (!currentUser) return;
    
    document.getElementById('profile-username').value = currentUser.username;
    document.getElementById('profile-email').value = currentUser.email;
}

// Handle update profile
async function handleUpdateProfile(e) {
    e.preventDefault();
    
    if (!currentUser) return;
    
    const email = document.getElementById('profile-email').value;
    const currentPassword = document.getElementById('profile-current-password').value;
    const newPassword = document.getElementById('profile-new-password').value;
    const confirmPassword = document.getElementById('profile-confirm-password').value;
    
    // Validate passwords match if provided
    if (newPassword && newPassword !== confirmPassword) {
        showMessage('profile-message', 'New passwords do not match.', 'danger');
        return;
    }
    
    try {
        showLoading('update-profile-btn');
        
        const response = await fetch(`${apiBaseUrl}/user/update-profile`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            },
            body: JSON.stringify({
                userId: currentUser.id,
                email,
                currentPassword,
                newPassword
            })
        });
        
        const data = await response.json();
        
        if (response.ok) {
            showMessage('profile-message', 'Profile updated successfully!', 'success');
            
            // Update current user
            currentUser.email = email;
            localStorage.setItem('user', JSON.stringify(currentUser));
            
            // Clear password fields
            document.getElementById('profile-current-password').value = '';
            document.getElementById('profile-new-password').value = '';
            document.getElementById('profile-confirm-password').value = '';
        } else {
            showMessage('profile-message', data.error || 'Failed to update profile.', 'danger');
        }
    } catch (error) {
        console.error('Error updating profile:', error);
        showMessage('profile-message', 'An error occurred while updating profile.', 'danger');
    } finally {
        hideLoading('update-profile-btn');
    }
}

// Load API keys
async function loadApiKeys() {
    if (!currentUser) return;
    
    try {
        showLoading('api-keys-container');
        
        const response = await fetch(`${apiBaseUrl}/user/api-keys?userId=${currentUser.id}`, {
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const data = await response.json();
        
        if (response.ok) {
            const apiKeysContainer = document.getElementById('api-keys-list');
            
            if (data.apiKeys && data.apiKeys.length > 0) {
                let html = '<div class="table-container"><table>';
                html += '<thead><tr><th>Name</th><th>Created</th><th>Last Used</th><th>Status</th><th>Actions</th></tr></thead>';
                html += '<tbody>';
                
                data.apiKeys.forEach(key => {
                    html += `<tr>
                        <td>${key.name}</td>
                        <td>${new Date(key.created).toLocaleString()}</td>
                        <td>${key.lastUsed ? new Date(key.lastUsed).toLocaleString() : 'Never'}</td>
                        <td>${key.isActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>'}</td>
                        <td>
                            <button class="btn btn-danger btn-sm" onclick="revokeApiKey(${key.id})">Revoke</button>
                        </td>
                    </tr>`;
                });
                
                html += '</tbody></table></div>';
                apiKeysContainer.innerHTML = html;
            } else {
                apiKeysContainer.innerHTML = '<div class="alert alert-info">You don\'t have any API keys yet. Create your first API key below.</div>';
            }
        } else {
            showMessage('api-keys-message', data.error || 'Failed to load API keys.', 'danger');
        }
    } catch (error) {
        console.error('Error loading API keys:', error);
        showMessage('api-keys-message', 'An error occurred while loading API keys.', 'danger');
    } finally {
        hideLoading('api-keys-container');
    }
}

// Handle add API key
async function handleAddApiKey(e) {
    e.preventDefault();
    
    if (!currentUser) return;
    
    const name = document.getElementById('api-key-name').value;
    const allowDomainManagement = document.getElementById('api-key-domain-management').checked;
    const allowProfileManagement = document.getElementById('api-key-profile-management').checked;
    const allowedIps = document.getElementById('api-key-allowed-ips').value;
    
    try {
        showLoading('add-api-key-btn');
        
        const response = await fetch(`${apiBaseUrl}/user/api-key`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            },
            body: JSON.stringify({
                userId: currentUser.id,
                name,
                allowDomainManagement,
                allowProfileManagement,
                allowedIps
            })
        });
        
        const data = await response.json();
        
        if (response.ok) {
            showMessage('add-api-key-message', 'API key created successfully!', 'success');
            
            // Show API key
            document.getElementById('api-key-result').classList.remove('hidden');
            document.getElementById('api-key-value').value = data.apiKey;
            
            // Clear form
            document.getElementById('add-api-key-form').reset();
            
            // Reload API keys
            loadApiKeys();
        } else {
            showMessage('add-api-key-message', data.error || 'Failed to create API key.', 'danger');
        }
    } catch (error) {
        console.error('Error creating API key:', error);
        showMessage('add-api-key-message', 'An error occurred while creating API key.', 'danger');
    } finally {
        hideLoading('add-api-key-btn');
    }
}

// Revoke API key
async function revokeApiKey(keyId) {
    if (!currentUser) return;
    
    if (!confirm('Are you sure you want to revoke this API key? This action cannot be undone.')) {
        return;
    }
    
    try {
        const response = await fetch(`${apiBaseUrl}/user/api-key/${keyId}?userId=${currentUser.id}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const data = await response.json();
        
        if (response.ok) {
            showMessage('api-keys-message', 'API key revoked successfully!', 'success');
            loadApiKeys();
        } else {
            showMessage('api-keys-message', data.error || 'Failed to revoke API key.', 'danger');
        }
    } catch (error) {
        console.error('Error revoking API key:', error);
        showMessage('api-keys-message', 'An error occurred while revoking API key.', 'danger');
    }
}

// Load billing
async function loadBilling() {
    if (!currentUser) return;
    
    try {
        // Load subscription plans
        const plansResponse = await fetch(`${apiBaseUrl}/payment/plans`, {
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const plansData = await plansResponse.json();
        
        if (plansResponse.ok) {
            const plansContainer = document.getElementById('subscription-plans');
            
            if (plansData.plans && plansData.plans.length > 0) {
                let html = '<div class="pricing">';
                
                plansData.plans.forEach(plan => {
                    const isCurrentPlan = currentUser.plan.toLowerCase() === plan.id.toLowerCase();
                    
                    html += `<div class="pricing-card ${isCurrentPlan ? 'featured' : ''}">
                        <h3>${plan.name}</h3>
                        <div class="price">$${plan.price.toFixed(2)}<span>/month</span></div>
                        <ul>`;
                    
                    plan.features.forEach(feature => {
                        html += `<li>${feature}</li>`;
                    });
                    
                    html += '</ul>';
                    
                    if (isCurrentPlan) {
                        html += '<button class="btn btn-secondary" disabled>Current Plan</button>';
                    } else if (plan.isEnabled) {
                        html += `<button class="btn btn-primary subscribe-btn" data-plan="${plan.id}">Subscribe</button>`;
                    } else {
                        html += '<button class="btn btn-secondary" disabled>Not Available</button>';
                    }
                    
                    html += '</div>';
                });
                
                html += '</div>';
                plansContainer.innerHTML = html;
                
                // Re-attach event listeners
                document.querySelectorAll('.subscribe-btn').forEach(btn => {
                    btn.addEventListener('click', handleSubscribe);
                });
            }
        }
        
        // Load transactions
        const transactionsResponse = await fetch(`${apiBaseUrl}/payment/transactions?userId=${currentUser.id}`, {
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
        });
        
        const transactionsData = await transactionsResponse.json();
        
        if (transactionsResponse.ok) {
            const transactionsContainer = document.getElementById('transactions-list');
            
            if (transactionsData.transactions && transactionsData.transactions.length > 0) {
                let html = '<div class="table-container"><table>';
                html += '<thead><tr><th>Date</th><th>Description</th><th>Amount</th><th>Status</th></tr></thead>';
                html += '<tbody>';
                
                transactionsData.transactions.forEach(transaction => {
                    html += `<tr>
                        <td>${new Date(transaction.transactionDate).toLocaleString()}</td>
                        <td>${transaction.description}</td>
                        <td>${transaction.currency} ${transaction.amount.toFixed(2)}</td>
                        <td>${transaction.status}</td>
                    </tr>`;
                });
                
                html += '</tbody></table></div>';
                transactionsContainer.innerHTML = html;
            } else {
                transactionsContainer.innerHTML = '<div class="alert alert-info">You don\'t have any transactions yet.</div>';
            }
        }
    } catch (error) {
        console.error('Error loading billing information:', error);
        showMessage('billing-message', 'An error occurred while loading billing information.', 'danger');
    }
}

// Handle subscribe
async function handleSubscribe(e) {
    if (!currentUser) return;
    
    const plan = e.target.getAttribute('data-plan');
    
    try {
        showLoading(e.target.id || 'subscribe-btn');
        
        const response = await fetch(`${apiBaseUrl}/payment/create-checkout`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${localStorage.getItem('token')}`
            },
            body: JSON.stringify({
                userId: currentUser.id,
                plan,
                successUrl: `${window.location.origin}/payment-success`,
                cancelUrl: `${window.location.origin}/payment-cancel`
            })
        });
        
        const data = await response.json();
        
        if (response.ok) {
            // Redirect to Stripe checkout
            window.location.href = `https://checkout.stripe.com/pay/${data.sessionId}`;
        } else {
            showMessage('billing-message', data.error || 'Failed to create checkout session.', 'danger');
        }
    } catch (error) {
        console.error('Error creating checkout session:', error);
        showMessage('billing-message', 'An error occurred while creating checkout session.', 'danger');
    } finally {
        hideLoading(e.target.id || 'subscribe-btn');
    }
}

// Update UI for logged in user
function updateUIForLoggedInUser() {
    // Show authenticated nav items
    document.querySelectorAll('.nav-auth').forEach(item => {
        item.classList.remove('hidden');
    });
    
    // Hide unauthenticated nav items
    document.querySelectorAll('.nav-unauth').forEach(item => {
        item.classList.add('hidden');
    });
    
    // Update user info
    const userInfoElements = document.querySelectorAll('.user-info');
    userInfoElements.forEach(element => {
        element.textContent = currentUser.username;
    });
    
    // Update plan info
    const planInfoElements = document.querySelectorAll('.plan-info');
    planInfoElements.forEach(element => {
        element.textContent = currentUser.plan;
    });
    
    // Show admin nav items if user is admin
    document.querySelectorAll('.nav-admin').forEach(item => {
        if (currentUser.isAdmin) {
            item.classList.remove('hidden');
        } else {
            item.classList.add('hidden');
        }
    });
}

// Update UI for logged out user
function updateUIForLoggedOutUser() {
    // Hide authenticated nav items
    document.querySelectorAll('.nav-auth').forEach(item => {
        item.classList.add('hidden');
    });
    
    // Show unauthenticated nav items
    document.querySelectorAll('.nav-unauth').forEach(item => {
        item.classList.remove('hidden');
    });
    
    // Hide admin nav items
    document.querySelectorAll('.nav-admin').forEach(item => {
        item.classList.add('hidden');
    });
}

// Show message
function showMessage(elementId, message, type) {
    const element = document.getElementById(elementId);
    if (element) {
        element.innerHTML = `<div class="alert alert-${type}">${message}</div>`;
        element.classList.remove('hidden');
        
        // Auto-hide after 5 seconds
        setTimeout(() => {
            element.classList.add('hidden');
        }, 5000);
    }
}

// Show loading
function showLoading(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.disabled = true;
        element.innerHTML = '<span class="spinner"></span> Loading...';
    }
}

// Hide loading
function hideLoading(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.disabled = false;
        element.innerHTML = element.getAttribute('data-original-text') || 'Submit';
    }
}

// Helper function to get client IP
async function getClientIp() {
    try {
        const response = await fetch(`${apiBaseUrl}/dynamicdns/client-ip`);
        const data = await response.json();
        
        if (response.ok) {
            return data;
        }
    } catch (error) {
        console.error('Error getting client IP:', error);
    }
    
    return null;
}