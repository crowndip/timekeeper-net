window.authenticateAdmin = async function(password) {
    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ password: password }),
            credentials: 'include'
        });
        
        const result = await response.json();
        return { success: result.success || false };
    } catch (error) {
        console.error('Authentication error:', error);
        return { success: false };
    }
};
